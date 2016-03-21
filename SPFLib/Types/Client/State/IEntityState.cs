namespace SPFLib.Types
{
    public interface IEntityState
    {
        short Health { get; }
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        Quaternion Rotation { get; }
    }
}
