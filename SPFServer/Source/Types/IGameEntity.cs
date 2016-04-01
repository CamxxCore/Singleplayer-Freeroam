using SPFLib.Types;

namespace SPFServer.Types
{
    public interface IGameEntity
    {
        Vector3 Position { get; set; }
        Quaternion Rotation { get; set; }
    }
}
