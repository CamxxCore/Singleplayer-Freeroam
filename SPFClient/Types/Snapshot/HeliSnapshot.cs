﻿using System;
using Vector3 = GTA.Math.Vector3;
using Quaternion = GTA.Math.Quaternion;

namespace SPFClient.Types
{
    public class HeliSnapshot
    {
        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Quaternion Rotation { get; private set; }
        public DateTime Timestamp { get; private set; }
        public float RotorSpeed { get; private set; }

        public HeliSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, float rotorSpeed, DateTime timestamp)
        {
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            RotorSpeed = rotorSpeed;
            Timestamp = timestamp;
        }

        public HeliSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation, float rotorSpeed) :
        this(position, velocity, rotation, rotorSpeed, new DateTime())
        { }

        public HeliSnapshot(Vector3 position, Vector3 velocity, Quaternion rotation) :
            this(position, velocity, rotation, 0f, new DateTime())
        { }

    }
}