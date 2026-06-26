using System;
using System.IO;
using System.Text;

namespace Pulsar4X.Client
{
    /// <summary>
    /// A log sink that rolls over to a fresh file when the current one fills up — like a chart recorder
    /// swapping to a clean roll of paper instead of writing until the page is unreadable.
    ///
    /// WHY THIS EXISTS: the session log writes ~5 lines every few seconds (heartbeat) plus every action and
    /// battle line, so a single <c>game_log.txt</c> from even a short play session grows past the size a reviewer
    /// (or any tool) can read in one go — it trips a "file too large" wall and you can only see part of it. This
    /// writer keeps every line, but splits the stream into numbered, READ-SIZED pages under a folder:
    /// <c>game_logs/game_log_000.txt</c>, <c>_001.txt</c>, … Each page is capped (see <see cref="MaxLines"/> /
    /// <see cref="MaxBytes"/>) just under that wall, so the whole session can be read start-to-finish, one page at
    /// a time, nothing lost.
    ///
    /// It's a drop-in <see cref="TextWriter"/>: <c>Program.cs</c> hands it to <c>Console.SetOut/SetError</c>, so
    /// EVERY <c>Console.WriteLine</c> in the whole client AND engine (the <c>[ACTION]</c>/<c>[DETECT]</c>/
    /// <c>[Combat]</c>… firehose, plus any exception dump on stderr) flows through it and gets paged.
    ///
    /// SAFETY: logging must never crash the game. Every file operation is wrapped; on the first I/O fault the
    /// writer latches into a fallback mode and quietly redirects to the real console (captured by
    /// <c>console_output.txt</c> via launch.bat), after printing one fault line there. It never throws back to the
    /// caller and never re-enters itself. Writes are serialized by a lock (engine processors log from parallel
    /// threads); each page is <c>AutoFlush</c> so a freeze or crash still leaves the full trail up to that instant.
    /// </summary>
    public sealed class RotatingLogWriter : TextWriter
    {
        /// <summary>Soft cap on lines per page. A page rolls when it passes EITHER this or <see cref="MaxBytes"/>,
        /// whichever comes first, and only at a line boundary (a single line never splits across two pages).
        /// Tuning, in plain terms: this is "how many lines per page." If a page ever still reads as "too large,"
        /// lower it; raise it if you'd rather have fewer, bigger files. ~1000 lines reads cleanly in one pass.</summary>
        public const int MaxLines = 1000;

        /// <summary>Hard cap on bytes per page (~120 KB) — the TRUE "too large" guard, since the wall is about total
        /// size, not line count. Protects against a page of few-but-very-long lines. Approximate (counts characters,
        /// and a few symbols like — ⚠ × are multi-byte in the file), so the real page may run a touch over; the
        /// margin to the read wall absorbs it. Lower this if pages ever read as too large.</summary>
        public const long MaxBytes = 120_000;

        readonly string _dir;
        readonly string _prefix;
        readonly TextWriter _fallback;   // the REAL console (pre-redirect), used only if a file op faults
        readonly object _lock = new object();

        StreamWriter _cur;
        int _fileIndex;
        int _lineCount;
        long _byteCount;
        bool _faulted;

        public override Encoding Encoding => Encoding.UTF8;

        /// <summary>Folder the pages are being written into (for the startup banner). Named LogDirectory, not
        /// Directory, so it never shadows System.IO.Directory inside this class.</summary>
        public string LogDirectory => _dir;

        /// <param name="dir">Folder to hold the pages (created if missing; existing pages from a prior run are
        /// cleared, so the folder always holds just THIS session — matching console_output.txt/game_log.txt, which
        /// also start fresh each launch).</param>
        /// <param name="prefix">Page filename prefix, e.g. "game_log_" → game_log_000.txt.</param>
        /// <param name="fallback">The real Console.Out, captured BEFORE redirection — where fault notices go.</param>
        public RotatingLogWriter(string dir, string prefix, TextWriter fallback)
        {
            _dir = dir;
            _prefix = prefix;
            _fallback = fallback;
            System.IO.Directory.CreateDirectory(_dir);
            // Start a clean series for this session — wipe stale pages from previous runs (best-effort).
            try
            {
                foreach (var f in System.IO.Directory.GetFiles(_dir, _prefix + "*.txt"))
                    File.Delete(f);
            }
            catch { /* a locked/again-open page just stays; the new series still opens below */ }
            Open();
        }

        void Open()
        {
            var path = Path.Combine(_dir, _prefix + _fileIndex.ToString("000") + ".txt");
            _cur = new StreamWriter(path, append: false) { AutoFlush = true };
            _lineCount = 0;
            _byteCount = 0;
        }

        void Roll()
        {
            try { _cur?.Flush(); _cur?.Dispose(); } catch { }
            _fileIndex++;
            Open();
        }

        public override void Write(char value) => Emit(value == '\0' ? string.Empty : value.ToString());
        public override void Write(string value) => Emit(value);

        void Emit(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            lock (_lock)
            {
                if (_faulted) { try { _fallback?.Write(s); } catch { } return; }
                try
                {
                    _cur.Write(s);
                    _byteCount += s.Length;
                    int newlines = 0;
                    foreach (var c in s) if (c == '\n') newlines++;
                    _lineCount += newlines;
                    // Roll only after a newline, so a line is never cut in half across two pages.
                    if (newlines > 0 && (_lineCount >= MaxLines || _byteCount >= MaxBytes))
                        Roll();
                }
                catch (Exception e)
                {
                    // Disk full, path gone, whatever — latch to fallback so the game keeps running and the log
                    // quietly continues on the real console (→ console_output.txt). One notice, then silent.
                    _faulted = true;
                    try { _fallback?.WriteLine("[RotatingLogWriter] file logging faulted, falling back to console: " + e.Message); } catch { }
                    try { _fallback?.Write(s); } catch { }
                }
            }
        }

        public override void Flush()
        {
            lock (_lock) { try { _cur?.Flush(); } catch { } }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { lock (_lock) { try { _cur?.Flush(); _cur?.Dispose(); } catch { } } }
            base.Dispose(disposing);
        }
    }
}
