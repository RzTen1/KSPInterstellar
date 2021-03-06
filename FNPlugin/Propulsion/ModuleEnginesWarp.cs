﻿using System;
using System.Linq;
using UnityEngine;
using FNPlugin.Extensions;

namespace FNPlugin
{
    public class ModuleEnginesWarp : ModuleEnginesFX
    {
        // GUI display values
        // Thrust
        [KSPField(isPersistant = true)]
        bool IsForceActivated;

        [KSPField(guiActive = true, guiName = "Warp Thrust")]
        protected string Thrust = "";
        // Isp
        [KSPField(guiActive = true, guiName = "Warp Isp")]
        protected string Isp = "";
        // Throttle
        [KSPField(guiActive = true, guiName = "Warp Throttle")]
        protected string Throttle = "";

        [KSPField(guiActive = true, guiName = "Demand")]
        public double propellantUsed;

        //[KSPField(guiActive = false, guiName = "Calc Flow")]
        //public double calcualtedFlow;

        [KSPField(guiActive = true, guiName = "Mass Flow")]
        public double requestedFlow;

        // Numeric display values
        protected double thrust_d = 0;
        protected double isp_d = 0;
        protected double throttle_d = 0;

        // Persistent values to use during timewarp
        float IspPersistent = 0;
        float ThrustPersistent = 0;
        float ThrottlePersistent = 0;
        float previousThrottle = 0;

        // Are we transitioning from timewarp to reatime?
        [KSPField]
        bool warpToReal = false;

        // Resource used for deltaV and mass calculations
        [KSPField]
        public string resourceDeltaV;
        // Density of resource
        double density;

        // Update
        public override void OnUpdate()
        {
            // When transitioning from timewarp to real update throttle
            if (warpToReal)
            {
                vessel.ctrlState.mainThrottle = ThrottlePersistent;
                warpToReal = false;
            }

            // Persistent thrust GUI
            Fields["Thrust"].guiActive = isEnabled;
            Fields["Isp"].guiActive = isEnabled;
            Fields["Throttle"].guiActive = isEnabled;

            // Update display values
            Thrust = FormatThrust(thrust_d);
            Isp = Math.Round(isp_d, 2).ToString() + " s";
            Throttle = Math.Round(throttle_d * 100).ToString() + "%";

            if (!IsForceActivated && isEnabled && isOperational)
            {
                IsForceActivated = true;
                part.force_activate();
            }
        }

        // Initialization
        public override void OnLoad(ConfigNode node)
        {
            // Run base OnLoad method
            base.OnLoad(node);

            // Initialize density of propellant used in deltaV and mass calculations
            density = PartResourceLibrary.Instance.GetDefinition(resourceDeltaV).density;
        }

        // Physics update
        public override void OnFixedUpdate()
        {
            if (FlightGlobals.fetch == null || !isEnabled) return;

            // Realtime mode
            if (!this.vessel.packed)
            {
                //double mdot = requestedMassFlow;
                requestedFlow = this.requestedMassFlow;
                //calcualtedFlow = ThrustPersistent / (IspPersistent * 9.81); // Mass burn rate of engine
                double dm = requestedFlow * TimeWarp.fixedDeltaTime; 
                propellantUsed = dm / density; // Resource demand

                // if not transitioning from warp to real
                // Update values to use during timewarp
                if (!warpToReal) //&& vessel.ctrlState.mainThrottle == previousThrottle)
                {
                    IspPersistent = realIsp;
                    ThrottlePersistent = vessel.ctrlState.mainThrottle;

                    this.CalculateThrust();
                    // verify we have thrust
                    if ((vessel.ctrlState.mainThrottle > 0 && finalThrust > 0) || (vessel.ctrlState.mainThrottle == 0 && finalThrust == 0))
                        ThrustPersistent = finalThrust;
                }
            }
            else if (part.vessel.situation != Vessel.Situations.SUB_ORBITAL)
            { 
                // Timewarp mode: perturb orbit using thrust
                warpToReal = true; // Set to true for transition to realtime
                double UT = Planetarium.GetUniversalTime(); // Universal time

                requestedFlow = this.requestedMassFlow;
                //calcualtedFlow = ThrustPersistent / (IspPersistent * 9.81); // Mass burn rate of engine
                double dm = requestedFlow * TimeWarp.fixedDeltaTime; // Change in mass over dT
                double demandReq = dm / density; // Resource demand
                // Update vessel resource
                propellantUsed = part.RequestResource(resourceDeltaV, demandReq);
                // Calculate thrust and deltaV if demand output > 0
                // TODO test if dm exceeds remaining propellant mass
                if (propellantUsed > 0)
                {
                    double vesselMass = this.vessel.GetTotalMass(); // Current mass
                    double m1 = vesselMass - dm; // Mass at end of burn
                    double deltaV = IspPersistent * PluginHelper.GravityConstant * Math.Log(vesselMass / m1); // Delta V from burn

                    Vector3d thrustV = this.part.transform.up; // Thrust direction
                    Vector3d deltaVV = deltaV * thrustV; // DeltaV vector
                    vessel.orbit.Perturb(deltaVV, UT, TimeWarp.fixedDeltaTime); // Update vessel orbit
                }
                // Otherwise, if throttle is turned on, and demand out is 0, show warning
                else if (ThrottlePersistent > 0)
                {
                    Debug.Log("Propellant depleted");
                }
            }
            else //if (vessel.ctrlState.mainThrottle > 0)
            {
                ScreenMessages.PostScreenMessage("Cannot accelerate and timewarp durring sub orbital spaceflight!", 5.0f, ScreenMessageStyle.UPPER_CENTER);
            }
            

            // Update display numbers
            thrust_d = ThrustPersistent;
            isp_d = IspPersistent;
            throttle_d = ThrottlePersistent;
            previousThrottle = vessel.ctrlState.mainThrottle;
        }

        // Format thrust into mN, N, kN
        public static string FormatThrust(double thrust)
        {
            if (thrust < 0.001)
                return Math.Round(thrust * 1000000.0, 3).ToString() + " mN";
            else if (thrust < 1.0)
                return Math.Round(thrust * 1000.0, 3).ToString() + " N";
            else
                return Math.Round(thrust, 3).ToString() + " kN";
        }
    }

}