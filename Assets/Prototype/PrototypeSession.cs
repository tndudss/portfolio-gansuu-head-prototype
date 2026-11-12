using System;
using UnityEngine;

namespace GansuuHead
{
    // Deterministic rules shared by gameplay and Pipeline verification.
    public sealed class PrototypeSession
    {
        public enum Phase { Ready, Playing, Success, Failure }
        public Phase State { get; private set; }
        public float Drowsiness { get; private set; }
        double elapsed;
        public float Elapsed => (float)elapsed;
        public float Duration { get; private set; }
        public float FailureThreshold { get; private set; } = 1;
        public void Start(int minutes, float failureThreshold = 1)
        {
            if (minutes != 1 && minutes != 3 && minutes != 5)
                throw new ArgumentOutOfRangeException(nameof(minutes));
            FailureThreshold = Mathf.Clamp(failureThreshold,.01f,1);
            Duration = minutes * 60f;
            elapsed = 0; Drowsiness = 0;
            State = Phase.Playing;
        }
        public void Tick(float seconds, float pixels, float accumulation, float reliefPerPixel, float clockScale = 1)
        {
            if (State != Phase.Playing || seconds <= 0) return;
            clockScale = Mathf.Max(1, clockScale);
            float dt = Mathf.Min(seconds, (float)(Duration - elapsed) / clockScale);
            float usedPixels = Mathf.Max(0, pixels) * dt / seconds;
            float rate = Mathf.Max(0, accumulation) - usedPixels / dt * Mathf.Max(0, reliefPerPixel);
            // Reaching the configured threshold at the deadline counts as failure.
            if (rate > 0 && (FailureThreshold - Drowsiness) / rate <= dt)
            {
                elapsed += (FailureThreshold - Drowsiness) / rate * clockScale;
                Drowsiness = FailureThreshold;
                State = Phase.Failure;
                return;
            }
            Drowsiness = Mathf.Clamp(Drowsiness + rate * dt, 0, FailureThreshold);
            elapsed += dt * clockScale;
            if (elapsed >= Duration - 0.00001) State = Phase.Success;
        }
        public void Recover(float amount, float minimumRemaining = 0)
        {
            if (State != Phase.Playing) return;
            Drowsiness = Mathf.Max(Mathf.Min(Drowsiness, Mathf.Max(0,minimumRemaining)),Drowsiness - Mathf.Max(0,amount));
        }
#if UNITY_EDITOR || DEBUG
        public void DeveloperSeek(float seconds)
        {
            if (State == Phase.Playing) elapsed = Mathf.Clamp(seconds,0,Duration-.001f);
        }
#endif
    }
}







