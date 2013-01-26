﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Xna.Framework;

namespace WebGame
{
    public abstract class Entity
    {
        public int Id { get; set; }

        public Game Game { get; set; }
        public StarSystem StarSystem { get; set; }

        public Vector3 Position { get; protected set; }
        public double Orientation { get; protected set; }
        public Vector3 Velocity { get; protected set; }
        public double MassMetricTons { get; set; }

        public abstract void Update(TimeSpan elapsed);

        public void ApplyVelocity()
        {
            Position += Velocity;
        }
    }
}