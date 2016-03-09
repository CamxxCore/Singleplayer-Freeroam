namespace SPFLib.Types
{
    public interface IEntityState
    {
        int ID { get; }
        short Health { get; }
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        Quaternion Rotation { get; }
    }
}
