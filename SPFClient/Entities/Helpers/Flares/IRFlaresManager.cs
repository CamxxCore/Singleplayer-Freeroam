using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Math;

namespace SPFClient.Entities
{
    /// <summary>
    /// Class library making it easy to implement IR countermeasures with ScriptHookVDotNet scripts.
    /// </summary>
    public sealed class IRFlareManager
    {
        /// <summary>
        /// Max time for dropping flares (ms). <b>Default =</b> 5200
        /// </summary>
        public int MaxDropTime { get; set; } = 3000;

        /// <summary>
        /// Frequency of flare drops (ms). <b>Default =</b> 250
        /// </summary>
        public int FlareDropInterval { get; set; } = 263;

        /// <summary>
        /// Initializes the class for the given vehicle.
        /// </summary>
        /// <param name="vehicle">Target vehicle</param>
        /// <returns></returns>
        public IRFlareManager SetupWithVehicle(Vehicle vehicle)
        {
            this.vehicle = vehicle;
            activeFlareSequence = new List<IRFlareSequence>();
            activeFlareSequence.Add(new IRFlareSequence(vehicle, FlareDropInterval, MaxDropTime, new Vector3(0, 0.6f, -2.0f), IRFlareSequence.ForceType.Left, 15f));
            activeFlareSequence.Add(new IRFlareSequence(vehicle, FlareDropInterval, MaxDropTime, new Vector3(0, -0.6f, -2.0f), IRFlareSequence.ForceType.Right, 15f));
            activeFlareSequence.Add(new IRFlareSequence(vehicle, FlareDropInterval, MaxDropTime, new Vector3(0, 0.4f, -2.0f), IRFlareSequence.ForceType.Left, 9f));
            activeFlareSequence.Add(new IRFlareSequence(vehicle, FlareDropInterval, MaxDropTime, new Vector3(0, -0.4f, -2.0f), IRFlareSequence.ForceType.Right, 9f));
            activeFlareSequence.Add(new IRFlareSequence(vehicle, FlareDropInterval, MaxDropTime, new Vector3(0, 0.0f, -2.0f), IRFlareSequence.ForceType.Down, 2.5f));
            return this;
        }

        private List<IRFlareSequence> activeFlareSequence;
        private Vehicle vehicle;

        /// <summary>
        /// Update the class.
        /// </summary>
        public void Update()
        {
            activeFlareSequence?.ForEach(x => x.Update());
        }

        /// <summary>
        /// Start flare sequence.
        /// </summary>
        public void Start()
        {
            RemoveAll();
            activeFlareSequence?.ForEach(x => x.Start());
        }

        /// <summary>
        /// Remove all flares.
        /// </summary>
        public void RemoveAll()
        {
            activeFlareSequence?.ForEach(x => x.RemoveAll());
        }
    }

    internal sealed class IRFlareSequence
    {
        private bool active;
        private Vehicle baseVehicle;
        private int timeout, dropTimeout;
        private int dropInterval, maxDropTime;
        private Vector3 dropOffset;
        private ForceType forceType;
        private float forceMultiplier;

        private List<IRFlare> activeFlares = new List<IRFlare>();

        /// <summary>
        /// Active flares collection
        /// </summary>
        public List<IRFlare> ActiveFlares { get { return activeFlares; } }

        /// <summary>
        /// Time between bomb drop (ms)
        /// </summary>
        public int DropInterval { get { return dropInterval; } }

        /// <summary>
        /// Initialize the class
        /// </summary>
        /// <param name="baseVehicle">Target vehicle</param>
        /// <param name="dropInterval">Interval between flares</param>
        /// <param name="maxDropTime">Max time to drop flares</param>
        /// <param name="dropOffset">Spawn offset relative to base vehicle</param>
        /// <param name="forceType">The type of directional force to apply on spawn</param>
        /// <param name="forceMultiplier">Force multiplier</param>
        public IRFlareSequence(Vehicle baseVehicle, int dropInterval, int maxDropTime, Vector3 dropOffset, ForceType forceType, float forceMultiplier)
        {
            this.baseVehicle = baseVehicle;
            this.dropInterval = dropInterval;
            this.maxDropTime = maxDropTime;
            this.dropOffset = dropOffset;
            this.forceType = forceType;
            this.forceMultiplier = forceMultiplier;
        }

        /// <summary>
        /// Start the flare drop sequence
        /// </summary>
        public void Start()
        {
            RemoveAll();
            timeout = Game.GameTime + dropInterval;
            dropTimeout = Game.GameTime + maxDropTime;
            active = true;
        }

        /// <summary>
        /// Remove all active flares
        /// </summary>
        public void RemoveAll()
        {
            if (activeFlares.Count > 0)
            {
                activeFlares.ForEach(x => x.Remove());
                activeFlares.Clear();
            }
        }

        /// <summary>
        /// Update the class
        /// </summary>
        public void Update()
        {
            if (activeFlares.Count > 0)
            {
                var nearbyProjectiles = World.GetNearbyEntities(baseVehicle.Position - baseVehicle.ForwardVector * 50, 50f)
                    .Where(x => x.Model.Hash == -1707997257);//2586970039

                foreach (var proj in nearbyProjectiles)
                {
                    proj.ApplyForce(-proj.RightVector * 5);
                }

                for (int i = 0; i < activeFlares.Count; i++)
                {
                    bool exists;
                    activeFlares[i].Update(out exists);

                    if (!exists)
                        activeFlares.RemoveAt(i);
                }
            }

            if (active)
            {
                if (Game.GameTime > dropTimeout)
                {
                    active = false;
                    return;
                }

                if (Game.GameTime > timeout)
                {
                    var rot = new Vector3(80.0f, 0.0f, baseVehicle.Heading + 180.0f);
                    var newFlare = new IRFlare(baseVehicle.GetOffsetInWorldCoords(dropOffset), rot);

                    Vector3 force = forceType == ForceType.Down ? -baseVehicle.UpVector :
                        forceType == ForceType.Left ? -baseVehicle.RightVector :
                        forceType == ForceType.Right ? baseVehicle.RightVector :
                        forceType == ForceType.Up ? baseVehicle.UpVector : Vector3.Zero;

                    newFlare.Velocity = activeFlares.Count < 1 ?
                        baseVehicle.Velocity + new Vector3(0f, 0f, -10f) :
                        activeFlares[activeFlares.Count - 1].Velocity;

                    newFlare.ApplyForce(force * forceMultiplier);
                    newFlare.StartFX();

                    activeFlares.Add(newFlare);

                    timeout = Game.GameTime + dropInterval;
                }
            }
        }

        public enum ForceType
        {
            Up,
            Down,
            Left,
            Right
        }
    }
}