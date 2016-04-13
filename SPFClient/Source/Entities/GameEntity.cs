using System;
using GTA;

namespace SPFClient.Entities
{
    public delegate void EntityChangedEventHandler(object sender, EntityChangedEventArgs e);

    public abstract class GameEntity : Entity, IDisposable
    {
        private TimeSpan totalTime;
        private int deadTicks, aliveTicks, waterTicks, totalTicks;
        private int spawnTime;

        /// <summary>
        /// Fired when the entity has been revived.
        /// </summary>
        public event EntityChangedEventHandler Alive;

        /// <summary>
        /// Fired when the entity has died.
        /// </summary>
        public event EntityChangedEventHandler Dead;

        /// <summary>
        /// Fired when the entity has entered water.
        /// </summary>
        public event EntityChangedEventHandler EnterWater;

        /// <summary>
        /// Fired when the ped has exited water.
        /// </summary>
        public event EntityChangedEventHandler ExitWater;

        /// <summary>
        /// Fired when the ped has entered a vehicle.
        /// </summary>
        public event EntityChangedEventHandler EnterVehicle;

        /// <summary>
        /// Fired when the ped has exited a vehicle.
        /// </summary>
        public event EntityChangedEventHandler ExitVehicle;


        /// <summary>
        /// Total entity ticks.
        /// </summary>
        public int TotalTicks
        {
            get { return totalTicks; }
        }

        /// <summary>
        /// Total time entity has been available to the script.
        /// </summary>
        public TimeSpan TotalTime
        {
            get { return totalTime; }
        }

        /// <summary>
        /// Total ticks for which the entity has been alive.
        /// </summary>
        public int AliveTicks
        {
            get { return aliveTicks; }
        }

        /// <summary>
        /// Total ticks for which the entity has been dead.
        /// </summary>
        public int DeadTicks
        {
            get { return deadTicks; }
        }

        /// <summary>
        /// Total ticks for which the entity has been in water.
        /// </summary>
        public int WaterTicks
        {
            get { return waterTicks; }
        }

        /// <summary>
        /// Total ticks for which the entity has been in water.
        /// </summary>
        public int VehicleTicks
        {
            get { return waterTicks; }
        }

        /// <summary>
        /// Initialize the entity for management.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public GameEntity(Entity Entity) : base(Entity.Handle)
        {
            spawnTime = Game.GameTime;
            totalTime = default(TimeSpan);
        }

        protected virtual void OnEnterVehicle(EntityChangedEventArgs e)
        {
            EnterVehicle?.Invoke(this, e);
        }

        protected virtual void OnExitVehicle(EntityChangedEventArgs e)
        {
            ExitVehicle?.Invoke(this, e);
        }

        /// <summary>
        /// Fired when the entity has died.
        /// </summary>
        protected virtual void OnDead(EntityChangedEventArgs e)
        {
            Dead?.Invoke(this, e);
        }

        protected virtual void OnAlive(EntityChangedEventArgs e)
        {
            Alive?.Invoke(this, e);
        }

        protected virtual void OnWaterEnter(EntityChangedEventArgs e)
        {
            EnterWater?.Invoke(this, e);
        }

        protected virtual void OnWaterExit(EntityChangedEventArgs e)
        {
            ExitWater?.Invoke(this, e);
        }

        /// <summary>
        /// Call this method each tick to update entity related information.
        /// </summary>
        public virtual void Update()
        {
            if (IsDead)
            {
                if (deadTicks == 0)
                    OnDead(new EntityChangedEventArgs(this));
                aliveTicks = 0;
                deadTicks++;
            }

            else
            {
                if (IsInWater)
                {
                    if (waterTicks == 0)
                        OnWaterEnter(new EntityChangedEventArgs(this));
                    waterTicks++;
                }
                else
                {
                    if (waterTicks > 0)
                        OnWaterExit(new EntityChangedEventArgs(this));
                    waterTicks = 0;
                }

                if (aliveTicks == 0)
                    OnAlive(new EntityChangedEventArgs(this));

                deadTicks = 0;
                aliveTicks++;
            }

            totalTicks++;
            totalTime = TimeSpan.FromMilliseconds(Game.GameTime - spawnTime);
        }

        public virtual void Dispose()
        {
            CurrentBlip?.Remove();
            Delete();
        }
    }
}
