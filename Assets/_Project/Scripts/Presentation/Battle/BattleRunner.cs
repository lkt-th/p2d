using System;
using System.Collections.Generic;
using P2D.Domain;
using UnityEngine;

namespace P2D.Presentation.Battle
{
    /// <summary>
    /// Drives a BattleSimulation in real time at its fixed 30 ticks/s (×1/×2/×3 speed) and
    /// forwards its events to the view. UI buttons call the input methods; nothing here
    /// changes battle rules, so what the player saw is exactly what a replay produces.
    /// </summary>
    public sealed class BattleRunner : MonoBehaviour
    {
        const float SecondsPerTick = 1f / BattleSimulation.TicksPerSecond;
        const int MaxTicksPerFrame = 12;

        [SerializeField, Range(1, 3)] int speed = 1;

        BattleSimulation simulation;
        int eventCursor;
        float accumulator;
        bool paused;

        public BattleSimulation Simulation => simulation;
        public bool IsRunning => simulation != null && simulation.Outcome == BattleOutcome.Running;
        public int Speed => speed;

        /// <summary>Every simulation event in order (damage numbers, telegraphs, kills…).</summary>
        public event Action<BattleEvent> EventRaised;

        /// <summary>Outcome plus the input log to send to the server for validation.</summary>
        public event Action<BattleOutcome, IReadOnlyList<BattleInput>> Finished;

        public void Begin(BattleSetup setup)
        {
            simulation = new BattleSimulation(setup);
            eventCursor = 0;
            accumulator = 0;
            paused = false;
        }

        public void UseSkill(int slot) { if (IsRunning) simulation.Enqueue(BattleInputType.UseSkill, slot); }
        public void UseCommand() { if (IsRunning) simulation.Enqueue(BattleInputType.UseCommand); }
        public void Dodge() { if (IsRunning) simulation.Enqueue(BattleInputType.Dodge); }
        public void SetTarget(int enemy) { if (IsRunning) simulation.Enqueue(BattleInputType.SetTarget, enemy); }
        public void SetAuto(bool on) { if (IsRunning) simulation.Enqueue(BattleInputType.SetAuto, on ? 1 : 0); }
        public void SetSpeed(int value) => speed = Mathf.Clamp(value, 1, 3);
        public void SetPaused(bool value) => paused = value;

        /// <summary>Finishes the rest of the battle instantly (sweep / skip).</summary>
        public void Skip()
        {
            if (!IsRunning) return;
            simulation.RunToEnd();
            Flush();
        }

        void Update()
        {
            if (!IsRunning || paused) return;

            accumulator += Time.deltaTime * speed;
            int steps = 0;
            while (accumulator >= SecondsPerTick && steps < MaxTicksPerFrame)
            {
                accumulator -= SecondsPerTick;
                steps++;
                if (!simulation.Step()) break;
            }
            // After a long hitch, drop the backlog instead of fast-forwarding the fight.
            if (steps == MaxTicksPerFrame) accumulator = 0;

            Flush();
        }

        void Flush()
        {
            var events = simulation.Events;
            while (eventCursor < events.Count)
            {
                var e = events[eventCursor++];
                if (EventRaised != null) EventRaised(e);
                if (e.type == BattleEventType.Ended && Finished != null)
                    Finished(simulation.Outcome, simulation.AppliedInputs);
            }
        }
    }
}
