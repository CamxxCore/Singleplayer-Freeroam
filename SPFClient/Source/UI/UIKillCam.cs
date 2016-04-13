using GTA;
using Vector3 = GTA.Math.Vector3;

namespace SPFClient.UI
{
    public class UIKillCam
    {
        private Camera mainCamera;

        private int killcamTimer;
        private int killcamLength;
        private bool killcamEnabled;

        public UIKillCam() : this(null, 4000)
        {
            mainCamera = null;
        }
        public UIKillCam(Camera camera, int killcamLength)
        {
            mainCamera = camera;
            this.killcamLength = killcamLength;
        }

        public void SetupKillcamWithTarget(Entity entity, int killcamLength)
        {
            mainCamera = World.CreateCamera(new Vector3(), new Vector3(), 60f);
            mainCamera.AttachTo(entity, new Vector3(-1f, 0, 0));
            mainCamera.PointAt(entity);
            killcamTimer = Game.GameTime + killcamLength;
            killcamEnabled = true;
        }

        public void Update()
        {
            if (killcamEnabled && Game.GameTime >= killcamTimer)
            {
                killcamEnabled = false;
                World.RenderingCamera = null;
                mainCamera.Destroy();      
            }
        }
    }
}
