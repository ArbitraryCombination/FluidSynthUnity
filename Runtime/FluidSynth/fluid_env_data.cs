namespace MidiPlayerTK
{

    /*
     * envelope data
     */
    public class fluid_env_data {
        public uint count;
        public float coeff;
        public float incr;
        public float min;
        public float max;

        public override string ToString() {
            return $"count:{count} coeff:{coeff} incr:{incr} min:{min} max:{max}";
        }
    }

    /* Indices for envelope tables */
    public enum fluid_voice_envelope_index : byte {
        FLUID_VOICE_ENVDELAY,
        FLUID_VOICE_ENVATTACK,
        FLUID_VOICE_ENVHOLD,
        FLUID_VOICE_ENVDECAY,
        FLUID_VOICE_ENVSUSTAIN,
        FLUID_VOICE_ENVRELEASE,
        FLUID_VOICE_ENVFINISHED,
        FLUID_VOICE_ENVLAST // Sentinel
    }
}
