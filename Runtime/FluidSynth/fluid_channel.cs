using System;

namespace FluidSynth {

    public class fluid_channel {

        public short key_pressure;
        public short channel_pressure;
        public short pitch_bend;
        public short pitch_wheel_sensitivity;

        // controller values
        public readonly short[] cc = new short[128];
        
        public void init_ctrl() {
            key_pressure = 0;
            channel_pressure = 0;
            pitch_bend = 0x2000; // Range is 0x4000, pitch bend wheel starts in centered position
            pitch_wheel_sensitivity = 2; // two semi-tones

            Array.Clear(cc, 0, cc.Length);

            // Volume / initial attenuation (MSB & LSB)
            cc[7] = 127;
            cc[39] = 0;

            // Pan (MSB & LSB)
            cc[10] = 64;

            // Expression (MSB & LSB)
            cc[11] = 127;
            cc[43] = 127;
        }
    }
}
