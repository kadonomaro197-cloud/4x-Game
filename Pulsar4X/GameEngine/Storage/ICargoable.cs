namespace Pulsar4X.Storage
{
    public interface ICargoable
    {
        int ID { get; }
        string UniqueID { get; }
        string CargoTypeID { get; }
        string Name { get; }
        long MassPerUnit { get; }
        double VolumePerUnit { get; }
    }
}