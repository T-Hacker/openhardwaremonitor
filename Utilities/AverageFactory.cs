using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenHardwareMonitor.Utilities
{
    public static class AverageFactory
    {
        public class Average
        {
            private float[] values; 
            private float total = 0;
            private int next = 0;

            internal Average(int numSamples)
            {
                values = new float[numSamples];
            }

            public float Mediansmooth(float newValue)
            {
                total = total - values[next] + newValue;
                values[next] = newValue;
                next = (next + 1) % values.Length;

                return total / values.Length;
            }
        }

        private static Dictionary<int, Average> instances = new Dictionary<int, Average>();

        public static Average GetInstance(int key, int numSamples)
        {
            if (!instances.ContainsKey(key))
                instances[key] = new Average(numSamples);

            return instances[key];
        }
    }
}
