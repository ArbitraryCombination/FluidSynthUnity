// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable RedundantCaseLabel
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiPlayerTK {

    /* for fluid_voice_add_mod */
    public enum fluid_voice_addorover_mod {
        FLUID_VOICE_OVERWRITE,
        FLUID_VOICE_ADD,
        FLUID_VOICE_DEFAULT
    }

    public enum fluid_voice_status {
        FLUID_VOICE_CLEAN,
        FLUID_VOICE_ON,
        FLUID_VOICE_SUSTAINED,
        FLUID_VOICE_OFF
    }
    
    public enum fluid_loop : byte {
        FLUID_UNLOOPED = 0,
        FLUID_LOOP_DURING_RELEASE = 1,
        FLUID_NOTUSED = 2,
        FLUID_LOOP_UNTIL_RELEASE = 3
    }
	
    /** Flags to choose the interpolation method */
    public enum fluid_interp {
        None, // no interpolation: Fastest, but questionable audio quality
        Linear, // Straight-line interpolation: A bit slower, reasonable audio quality
        Cubic, // Fourth-order interpolation: Requires 50 % of the whole DSP processing time, good quality
        Order7,
    }

    public class fluid_voice {
        public const uint Nano100ToMilli = 10000;
        
        public const int FLUID_BUFSIZE = 64;

        // min vol envelope release (to stop clicks) in SoundFont timecents : ~16ms
        private const int NO_CHANNEL = 0xff;

        /* these should be the absolute minimum that FluidSynth can deal with */
        private const int FLUID_MIN_LOOP_SIZE = 2;
        private const int FLUID_MIN_LOOP_PAD = 0;

        /* min vol envelope release (to stop clicks) in SoundFont timecents */
        private const float FLUID_MIN_VOLENVRELEASE = -7200.0f;/* ~16ms */
        
        /// Multiplier to increase or decrease the default release time defined in the SoundFont.
        /// Recommended values between 0.1 and 2. 1 means no modification of the release time.
        private const float MPTK_ReleaseTimeMod = 1f;
        
        /// When amplitude is below this value the playing of sample is stopped (voice_off).
        /// Can be increase for better performance but with degraded quality because sample could be stopped earlier.
        /// Amplitude is a value in [0,1] range, so meaningful values for this are in [0.01, 0.5].
        private const float MPTK_CutOffVolume = 0.05f;
        
        private const fluid_interp InterpolationMethod = fluid_interp.Linear;

        private static readonly fluid_gen_type[] list_of_generators_to_initialize =
             {
                fluid_gen_type.GEN_STARTADDROFS,                    /* SF2.01 page 48 #0  - Unity load wave from wave file, no real time change possible on wave attribute */
                fluid_gen_type.GEN_ENDADDROFS,                      /*                #1  - Unity load wave from wave file, no real time change possible on wave attribute */
                fluid_gen_type.GEN_STARTLOOPADDROFS,                /*                #2  - Unity load wave from wave file, no real time change possible on wave attribute */
                fluid_gen_type.GEN_ENDLOOPADDROFS,                  /*                #3  - Unity load wave from wave file, no real time change possible on wave attribute */
                /* fluid_gen_type.GEN_STARTADDRCOARSEOFS see comment below [1]        #4  - Unity load wave from wave file, no real time change possible on wave attribute */
                fluid_gen_type.GEN_MODLFOTOPITCH,                   /*                #5   */
                fluid_gen_type.GEN_VIBLFOTOPITCH,                   /*                #6   */
                fluid_gen_type.GEN_MODENVTOPITCH,                   /*                #7   */
                fluid_gen_type.GEN_FILTERFC,                        /*                #8   */
                fluid_gen_type.GEN_FILTERQ,                         /*                #9   */
                fluid_gen_type.GEN_MODLFOTOFILTERFC,                /*                #10  */
                fluid_gen_type.GEN_MODENVTOFILTERFC,                /*                #11  */
                /* fluid_gen_type.GEN_ENDADDRCOARSEOFS [1]                            #12  - Unity load wave from wave file, no real time change possible on wave attribute */
                fluid_gen_type.GEN_MODLFOTOVOL,                     /*                #13  */
                /* not defined                                         #14  */
                fluid_gen_type.GEN_CHORUSSEND,                      /*                #15  */
                fluid_gen_type.GEN_REVERBSEND,                      /*                #16  */
                fluid_gen_type.GEN_PAN,                             /*                #17  */
                /* not defined                                         #18  */
                /* not defined                                         #19  */
                /* not defined                                         #20  */
                fluid_gen_type.GEN_MODLFODELAY,                     /*                #21  */
                fluid_gen_type.GEN_MODLFOFREQ,                      /*                #22  */
                fluid_gen_type.GEN_VIBLFODELAY,                     /*                #23  */
                fluid_gen_type.GEN_VIBLFOFREQ,                      /*                #24  */
                fluid_gen_type.GEN_MODENVDELAY,                     /*                #25  */
                fluid_gen_type.GEN_MODENVATTACK,                    /*                #26  */
                fluid_gen_type.GEN_MODENVHOLD,                      /*                #27  */
                fluid_gen_type.GEN_MODENVDECAY,                     /*                #28  */
                /* fluid_gen_type.GEN_MODENVSUSTAIN [1]                               #29  */
                fluid_gen_type.GEN_MODENVRELEASE,                   /*                #30  */
                /* fluid_gen_type.GEN_KEYTOMODENVHOLD [1]                             #31  */
                /* fluid_gen_type.GEN_KEYTOMODENVDECAY [1]                            #32  */
                fluid_gen_type.GEN_VOLENVDELAY,                     /*                #33  */
                fluid_gen_type.GEN_VOLENVATTACK,                    /*                #34  */
                fluid_gen_type.GEN_VOLENVHOLD,                      /*                #35  */
                fluid_gen_type.GEN_VOLENVDECAY,                     /*                #36  */
                /* fluid_gen_type.GEN_VOLENVSUSTAIN [1]                               #37  */
                fluid_gen_type.GEN_VOLENVRELEASE,                   /*                #38  */
                /* fluid_gen_type.GEN_KEYTOVOLENVHOLD [1]                             #39  */
                /* fluid_gen_type.GEN_KEYTOVOLENVDECAY [1]                            #40  */
                /* fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS [1]                      #45 - Unity load wave from wave file, no real time change possible on wave attribute */
                fluid_gen_type.GEN_KEYNUM,                          /*                #46  */
                fluid_gen_type.GEN_VELOCITY,                        /*                #47  */
                fluid_gen_type.GEN_ATTENUATION,                     /*                #48  */
                /* fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS [1]                        #50  - Unity load wave from wave file, no real time change possible on wave attribute */
                /* fluid_gen_type.GEN_COARSETUNE           [1]                        #51  */
                /* fluid_gen_type.GEN_FINETUNE             [1]                        #52  */
                fluid_gen_type.GEN_OVERRIDEROOTKEY,                 /*                #58  */
                fluid_gen_type.GEN_PITCH,                           /*                ---  */
             };

        private bool IsLoop; // TODO This seems fishy too
        
        //TODO This is a wrong place for this
        /** For how many more samples should the note be off before it starts. */
        private int remainingDelaySamples;
        /** For how many more samples should the note be on. */
        private int remainingNoteSamples;

        public fluid_voice_status status = fluid_voice_status.FLUID_VOICE_CLEAN;
        public int chan = NO_CHANNEL;             /* the channel number, quick access for channel messages */
        public int key = 0;              /* the key, quick acces for noteoff */
        private int vel = 0;              /* the velocity */
        private fluid_channel midiChannel = null;
        public readonly HiGen[] gens = new HiGen[(byte) fluid_gen_type.GEN_LAST];
        private readonly List<HiMod> mods = new List<HiMod>(); //[FLUID_NUM_MOD];
        public bool has_looped;                 /* Flag that is set as soon as the first loop is completed. */
        public HiSample sample = null;
        private int check_sample_sanity_flag;   /* Flag that initiates, that sample-related parameters have to be checked. */
        public float output_rate;        /* the sample rate of the synthesizer */

        private uint FluidTicks; // From fluidsynth (named ticks). Augmented of BUFSIZE at each call of write.

        public float amp;                /* current linear amplitude */
        public ulong /*fluid_phase_t*/ phase;             /* the phase of the sample wave */

        /* Temporary variables used in fluid_voice_write() */

        public float phase_incr;    /* the phase increment for the next 64 samples */
        public float amp_incr;      /* amplitude increment value */
        public readonly float[] dsp_buf = new float[FLUID_BUFSIZE];      /* buffer to store interpolated sample data to */

        /* End temporary variables */

        /* basic parameters */
        private float pitch;    /* the pitch in midicents */
        private float attenuation;        /* the attenuation in centibels */
        private float min_attenuation_cB; /* Estimate on the smallest possible attenuation during the lifetime of the voice */
        private float root_pitch;

        /* sample and loop start and end points (offset in sample memory).  */
        public int start;
        public int end;
        public int loopstart;
        public int loopend;    /* Note: first point following the loop (superimposed on loopstart) */

        /// <summary>
        /// volume enveloppe
        /// </summary>
        public readonly fluid_env_data[] volenv_data = new fluid_env_data[(byte)fluid_voice_envelope_index.FLUID_VOICE_ENVLAST]; //[FLUID_VOICE_ENVLAST];

        /// <summary>
        /// Count time since the start of the section
        /// </summary>
        private long volenv_count;

        /// <summary>
        /// Current section in the enveloppe
        /// </summary>
        public fluid_voice_envelope_index volenv_section;

        private float volenv_val;

        /* mod env */
        private readonly fluid_env_data[] modenv_data = new fluid_env_data[(byte) fluid_voice_envelope_index.FLUID_VOICE_ENVLAST];
        private long modenv_count;
        private fluid_voice_envelope_index modenv_section;
        private float modenv_val;         /* the value of the modulation envelope */
        private float modenv_to_pitch;

        /* mod lfo */
        private float modlfo_val;          /* the value of the modulation LFO */
        private uint modlfo_delay;       /* the delay of the lfo in samples */
        private float modlfo_incr;         /* the lfo frequency is converted to a per-buffer increment */
        private float modlfo_to_pitch;
        private float modlfo_to_vol;

        /* vib lfo */
        private float viblfo_val;        /* the value of the vibrato LFO */
        private long viblfo_delay;      /* the delay of the lfo in samples */
        private float viblfo_incr;       /* the lfo frequency is converted to a per-buffer increment */
        private float viblfo_to_pitch;

        public fluid_voice() {
            for (int i = 0; i < gens.Length; i++) {
                gens[i] = new HiGen { type = (fluid_gen_type) i };
            }

            for (int i = 0; i < modenv_data.Length; i++)
                modenv_data[i] = new fluid_env_data();

            for (int i = 0; i < volenv_data.Length; i++)
                volenv_data[i] = new fluid_env_data();

            // The 'sustain' and 'finished' segments of the volume / modulation envelope are constant. 
            // They are never affected by any modulator or generator. 
            // Therefore it is enough to initialize them once during the lifetime of the synth.

            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].count = 0xffffffff; // infiny until note off or duration is over
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].coeff = 1;
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].incr = 0;          // Volume remmains constant during sustain phase
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].min = -1;          // not used for sustain (constant volume)
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].max = 2; //1;     // not used for sustain (constant volume)

            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].count = 0xffffffff;
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].coeff = 0;
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].incr = 0;
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].min = -1;
            volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].max = 1;

            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].count = 0xffffffff;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].coeff = 1;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].incr = 0;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].min = -1;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN].max = 2; //1; fluidsythn original value=2

            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].count = 0xffffffff;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].coeff = 0;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].incr = 0;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].min = -1;
            modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED].max = 1;
        }

        /// Defined default voice value. Called also when a voice is reused.
        public void fluid_voice_init(float outputRate, int channum, fluid_channel pchannel, int pkey, int pvel, HiSample sample) {
            IsLoop = false;
            remainingDelaySamples = 0;
            status = fluid_voice_status.FLUID_VOICE_CLEAN;
            chan = channum;
            key = pkey;
            vel = pvel;
            midiChannel = pchannel;
            foreach (HiGen t in gens) {
                t.flags = fluid_gen_flags.GEN_UNUSED;
            }
            mods.Clear();
            has_looped = false; /* Will be set during voice_write when the 2nd loop point is reached */
            this.sample = sample;
            check_sample_sanity_flag = 0;
            output_rate = outputRate;
            FluidTicks = 0;
            amp = 0; /* The last value of the volume envelope, used to calculate the volume increment during processing */
            phase = 0;
            phase_incr = 0;
            amp_incr = 0;
            //Array.Clear(dsp_buf, 0, dsp_buf.Length);//NOTE(jp): Probably not necessary
            pitch = 0f;
            attenuation = 0f;
            min_attenuation_cB = 0f;
            root_pitch = 0f;
            start = 0;
            end = 0;
            loopstart = 0;
            loopend = 0;
            /* vol env initialization */
            for (var i = 0; i < volenv_data.Length; i++) {
                if (i == (int) fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN ||
                    i == (int) fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED) {
                    continue;
                }
                var vd = volenv_data[i];
                vd.count = 0;
                vd.coeff = 0;
                vd.incr = 0;
                vd.min = 0;
                vd.max = 0;
            }
            volenv_count = 0;
            volenv_section = 0;
            volenv_val = 0f;

            /* mod env initialization*/
            for (var i = 0; i < modenv_data.Length; i++) {
                if (i == (int) fluid_voice_envelope_index.FLUID_VOICE_ENVSUSTAIN ||
                    i == (int) fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED) {
                    continue;
                }
                var vd = modenv_data[i];
                vd.count = 0;
                vd.coeff = 0;
                vd.incr = 0;
                vd.min = 0;
                vd.max = 0;
            }
            modenv_count = 0;
            modenv_section = 0;
            modenv_val = 0;
            modenv_to_pitch = 0;

            /* mod lfo */
            modlfo_val = 0;/* Fixme: Retrieve from any other existing voice on this channel to keep LFOs in unison? */
            modlfo_delay = 0;
            modlfo_incr = 0;
            modlfo_to_pitch = 0;
            modlfo_to_vol = 0;

            /* vib lfo */
            viblfo_val = 0;/* Fixme: See mod lfo */
            viblfo_delay = 0;
            viblfo_incr = 0;
            viblfo_to_pitch = 0;

            /* Set all the generators to their default value, according to SF
             * 2.01 section 8.1.3 (page 48). The value of NRPN messages are
             * copied from the channel to the voice's generators. The sound font
             * loader overwrites them. The generator values are later converted
             * into voice parameters in
             * fluid_voice_calculate_runtime_synthesis_parameters.  */
            fluid_gen_info.fluid_gen_set_default_values(gens);
            /* For a looped sample, this value will be overwritten as soon as the
             * loop parameters are initialized (they may depend on modulators).
             * This value can be kept, it is a worst-case estimate.
             */

            /* add the default modulators to the synthesis process. */
            HiModDefault.AddDefaultMods(this);
        }


        /// <summary>
        ///  Adds a modulator to the voice.  "mode" indicates, what to do, if an identical modulator exists already.
        /// mode == FLUID_VOICE_ADD: Identical modulators on preset level are added
        /// mode == FLUID_VOICE_OVERWRITE: Identical modulators on instrument level are overwritten
        /// mode == FLUID_VOICE_DEFAULT: This is a default modulator, there can be no identical modulator.Don't check.
        /// </summary>
        /// <param name="pmod"></param>
        /// <param name="mode"></param>
        public void fluid_voice_add_mod(HiMod pmod, fluid_voice_addorover_mod mode) {
            /*
             * Some soundfonts come with a huge number of non-standard
             * controllers, because they have been designed for one particular
             * sound card.  Discard them, maybe print a warning.
             */

            if ((pmod.Flags1 & (byte)fluid_mod_flags.FLUID_MOD_CC) == 0 &&
                pmod.Src1 != 0 && pmod.Src1 != 2 && pmod.Src1 != 3 && pmod.Src1 != 10 && pmod.Src1 != 13 && pmod.Src1 != 14 && pmod.Src1 != 16
                ) {    /* Pitch wheel sensitivity */
                Debug.LogFormat("Ignoring invalid controller, using non-CC source {0}.", pmod.Src1);
                return;
            }

            if (mode == fluid_voice_addorover_mod.FLUID_VOICE_ADD ||
                mode == fluid_voice_addorover_mod.FLUID_VOICE_OVERWRITE) {
                foreach (HiMod mod1 in this.mods) {
                    /* if identical modulator exists, add them */
                    //fluid_mod_test_identity(mod1, mod))
                    if (mod1.Dest == pmod.Dest &&
                        mod1.Src1 == pmod.Src1 &&
                        mod1.Src2 == pmod.Src2 &&
                        mod1.Flags1 == pmod.Flags1 &&
                        mod1.Flags2 == pmod.Flags2) {
                        if (mode == fluid_voice_addorover_mod.FLUID_VOICE_ADD)
                            mod1.Amount += pmod.Amount;
                        else
                            mod1.Amount = pmod.Amount;
                        return;
                    }
                }
            }

            // Add a new modulator (No existing modulator to add / overwrite).
            // Also, default modulators (FLUID_VOICE_DEFAULT) are added without checking, 
            // if the same modulator already exists. 
            if (this.mods.Count < HiMod.FLUID_NUM_MOD) {
                HiMod mod1 = new HiMod {
                    Amount = pmod.Amount,
                    Dest = pmod.Dest,
                    Flags1 = pmod.Flags1,
                    Flags2 = pmod.Flags2,
                    Src1 = pmod.Src1,
                    Src2 = pmod.Src2
                };
                this.mods.Add(mod1);
            }
        }


        public void fluid_voice_start(long delayMs, long durationMs) {
            // The maximum volume of the loop is calculated and cached once for each sample with its nominal loop settings. 
            // This happens, when the sample is used for the first time.
            fluid_voice_calculate_runtime_synthesis_parameters();
            // Precalculate env. volume
            fluid_env_data env_data = volenv_data[(int)volenv_section];
            while (env_data.count <= 0d && (int)volenv_section < volenv_data.Length) {
                float lastmax = env_data.max;
                volenv_section++;
                env_data = volenv_data[(int)volenv_section];
                //volenv_count = 0d;
                volenv_val = lastmax;
            }

            // Precalculate env. modulation
            env_data = modenv_data[(int)modenv_section];
            while (env_data.count <= 0d && (int)modenv_section < modenv_data.Length) {
                float lastmax = env_data.max;
                modenv_section++;
                env_data = modenv_data[(int)modenv_section];
                modenv_count = 0;
                modenv_val = lastmax;
            }

            IsLoop = (byte)gens[(int)fluid_gen_type.GEN_SAMPLEMODE].Val == (byte)fluid_loop.FLUID_LOOP_UNTIL_RELEASE || (byte)gens[(int)fluid_gen_type.GEN_SAMPLEMODE].Val == (byte)fluid_loop.FLUID_LOOP_DURING_RELEASE;

            // Force setting of the phase at the first DSP loop run This cannot be done earlier, because it depends on modulators.
            check_sample_sanity_flag = 1 << 1; //#define FLUID_SAMPLESANITY_STARTUP (1 << 1) 

            // Voice with status FLUID_VOICE_ON are played in background when CorePlayer is enabled
            status = fluid_voice_status.FLUID_VOICE_ON;
            remainingDelaySamples = (int) (delayMs * output_rate / 1000f);
            remainingNoteSamples = durationMs < 0 ? -1 : (int) (durationMs * output_rate / 1000f);
        }

        /// <summary>
        /// in this function we calculate the values of all the parameters. the parameters are converted to their most useful unit for the DSP algorithm, 
        /// for example, number of samples instead of timecents.
        /// Some parameters keep their "perceptual" unit and conversion will be done in the DSP function.
        /// This is the case, for example, for the pitch since it is modulated by the controllers in cents.
        /// </summary>
        void fluid_voice_calculate_runtime_synthesis_parameters() {
            // When the voice is made ready for the synthesis process, a lot of voice-internal parameters have to be calculated.
            // At this point, the sound font has already set the -nominal- value for all generators (excluding GEN_PITCH). 
            // Most generators can be modulated - they include a nominal value and an offset (which changes with velocity, note number, channel parameters like
            // aftertouch, mod wheel...) 
            // Now this offset will be calculated as follows:
            //  - Process each modulator once.
            //  - Calculate its output value.
            //  - Find the target generator.
            //  - Add the output value to the modulation value of the generator.
            // Note: The generators have been initialized with fluid_gen_set_default_values.

            foreach (HiMod m in mods) {
                gens[m.Dest].Mod += m.fluid_mod_get_value(midiChannel, key, vel);
            }

            // The GEN_PITCH is a hack to fit the pitch bend controller into the modulator paradigm.  
            // Now the nominal pitch of the key is set.
            // Note about SCALETUNE: SF2.01 8.1.3 says, that this generator is a non-realtime parameter. So we don't allow modulation (as opposed
            // to _GEN(voice, GEN_SCALETUNE) When the scale tuning is varied, one key remains fixed. Here C3 (MIDI number 60) is used.
            gens[(int)fluid_gen_type.GEN_PITCH].Val = (gens[(int)fluid_gen_type.GEN_SCALETUNE].Val * (key - 60.0f) + 100.0f * 60.0f);

            /* Now the generators are initialized, nominal and modulation value.
             * The voice parameters (which depend on generators) are calculated
             * with fluid_voice_update_param. Processing the list of generator
             * changes will calculate each voice parameter once.
             *
             * Note [1]: Some voice parameters depend on several generators. For
             * example, the pitch depends on GEN_COARSETUNE, GEN_FINETUNE and
             * GEN_PITCH.  voice.pitch.  Unnecessary recalculation is avoided
             * by removing all but one generator from the list of voice
             * parameters.  Same with GEN_XXX and GEN_XXXCOARSE: the
             * initialisation list contains only GEN_XXX.
             */

            // Calculate the voice parameter(s) dependent on each generator.
            foreach (fluid_gen_type igen in list_of_generators_to_initialize)
                fluid_voice_update_param(igen);

            // Make an estimate on how loud this voice can get at any time (attenuation). */
            min_attenuation_cB = fluid_voice_get_lower_boundary_for_attenuation();
        }


        /*
         * fluid_voice_get_lower_boundary_for_attenuation
         *
         * Purpose:
         *
         * A lower boundary for the attenuation (as in 'the minimum
         * attenuation of this voice, with volume pedals, modulators
         * etc. resulting in minimum attenuation, cannot fall below x cB) is
         * calculated.  This has to be called during fluid_voice_init, after
         * all modulators have been run on the voice once.  Also,
         * voice.attenuation has to be initialized.
         */
        float fluid_voice_get_lower_boundary_for_attenuation() {
            float possible_att_reduction_cB = 0;

            foreach (HiMod m in mods) {
                // Modulator has attenuation as target and can change over time? 
                if ((m.Dest == (int)fluid_gen_type.GEN_ATTENUATION)
                    && ((m.Flags1 & (byte)fluid_mod_flags.FLUID_MOD_CC) > 0 || (m.Flags2 & (byte)fluid_mod_flags.FLUID_MOD_CC) > 0))
                {

                    float current_val = m.fluid_mod_get_value(midiChannel, key, vel);
                    float v = Mathf.Abs(m.Amount);

                    if ((m.Src1 == (byte)fluid_mod_src.FLUID_MOD_PITCHWHEEL)
                        || (m.Flags1 & (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR) > 0
                        || (m.Flags2 & (byte)fluid_mod_flags.FLUID_MOD_BIPOLAR) > 0
                        || (m.Amount < 0))
                    {
                        /* Can this modulator produce a negative contribution? */
                        v *= -1f;
                    }
                    else
                    {
                        /* No negative value possible. But still, the minimum contribution is 0. */
                        v = 0f;
                    }

                    /* For example:
                     * - current_val=100
                     * - min_val=-4000
                     * - possible_att_reduction_cB += 4100
                     */
                    if (current_val > v)
                    {
                        possible_att_reduction_cB += (current_val - v);
                    }
                }
            }

            float lower_bound = attenuation - possible_att_reduction_cB;

            /* SF2.01 specs do not allow negative attenuation */
            if (lower_bound < 0f)
            {
                lower_bound = 0f;
            }
            return lower_bound;
        }
        /// The value of a generator (gen) has changed.  (The different generators are listed in fluidsynth.h, or in SF2.01 page 48-49). Now the dependent 'voice' parameters are calculated.
        /// fluid_voice_update_param can be called during the setup of the  voice (to calculate the initial value for a voice parameter), or
        /// during its operation (a generator has been changed due to real-time parameter modifications like pitch-bend).
        /// Note: The generator holds three values: The base value .val, an offset caused by modulators .mod, and an offset caused by the
        /// NRPN system. _GEN(voice, generator_enumerator) returns the sum of all three.
        /// From fluid_midi_send_event NOTE_ON -. synth_noteon -. fluid_voice_start -. fluid_voice_calculate_runtime_synthesis_parameters
        /// From fluid_midi_send_event CONTROL_CHANGE -. fluid_synth_cc -. fluid_channel_cc Default      -. fluid_synth_modulate_voices     -. fluid_voice_modulate
        /// From fluid_midi_send_event CONTROL_CHANGE -. fluid_synth_cc -. fluid_channel_cc ALL_CTRL_OFF -. fluid_synth_modulate_voices_all -. fluid_voice_modulate_all
        private void fluid_voice_update_param(fluid_gen_type igen) {
            float genVal = CalculateGeneratorValue(igen);
            switch (igen) {
                case fluid_gen_type.GEN_PAN:
                    break;

                case fluid_gen_type.GEN_ATTENUATION:
                    // Range: SF2.01 section 8.1.3 # 48 Motivation for range checking:OHPiano.SF2 sets initial attenuation to a whooping -96 dB
                    attenuation = genVal < 0.0f ? 0.0f : genVal > 14440.0f ? 1440.0f : genVal;
                    break;

                // The pitch is calculated from the current note 
                case fluid_gen_type.GEN_PITCH:
                case fluid_gen_type.GEN_COARSETUNE:
                case fluid_gen_type.GEN_FINETUNE:
                    // The testing for allowed range is done in 'fluid_ct2hz' 
                    pitch = CalculateGeneratorValue(fluid_gen_type.GEN_PITCH) +
                            CalculateGeneratorValue(fluid_gen_type.GEN_COARSETUNE) * 100f +
                            CalculateGeneratorValue(fluid_gen_type.GEN_FINETUNE);
                    break;

                case fluid_gen_type.GEN_REVERBSEND:
                    break;
                case fluid_gen_type.GEN_CHORUSSEND:
                    break;
                case fluid_gen_type.GEN_OVERRIDEROOTKEY:
                    // This is a non-realtime parameter. Therefore the .mod part of the generator can be neglected.
                    //* NOTE: origpitch sets MIDI root note while pitchadj is a fine tuning amount which offsets the original rate.  
                    // This means that the fine tuning is inverted with respect to the root note (so subtract it, not add).
                    if (genVal > -1)
                    {
                        //FIXME: use flag instead of -1
                        root_pitch = genVal * 100.0f - sample.PitchAdj;
                    }
                    else
                    {
                        root_pitch = sample.OrigPitch * 100.0f - sample.PitchAdj;
                    }

                    root_pitch = fluid_conv.fluid_ct2hz(root_pitch);
                    root_pitch *= output_rate / sample.SampleRate;
                    break;

                case fluid_gen_type.GEN_FILTERFC:
                    break;

                case fluid_gen_type.GEN_FILTERQ:
                    break;

                case fluid_gen_type.GEN_MODLFOTOPITCH:
                    modlfo_to_pitch = genVal < -12000f ? -12000f : genVal > 12000f ? 12000f : genVal;
                    break;

                case fluid_gen_type.GEN_MODLFOTOVOL:
                    modlfo_to_vol = genVal < -960f ? -960f : genVal > 960f ? 960f : genVal;
                    break;

                case fluid_gen_type.GEN_MODLFOTOFILTERFC:
                    break;

                case fluid_gen_type.GEN_MODLFODELAY:
                    {
                        float x = genVal < -12000f ? -12000f : genVal > 5000f ? 5000f : genVal;
                        modlfo_delay = (uint)(output_rate * fluid_conv.fluid_tc2sec_delay(x));
                    }
                    break;

                case fluid_gen_type.GEN_MODLFOFREQ:
                    {
                        //the frequency is converted into a delta value, per buffer of FLUID_BUFSIZE samples - the delay into a sample delay
                        float x = genVal < -16000.0f ? -16000.0f : genVal > 4500.0f ? 4500.0f : genVal;
                        modlfo_incr = (4.0f * FLUID_BUFSIZE * fluid_conv.fluid_act2hz(x) / output_rate);
                    }
                    break;

                case fluid_gen_type.GEN_VIBLFOFREQ:
                    {
                        // the frequency is converted into a delta value, per buffer of FLUID_BUFSIZE samples the delay into a sample delay
                        float x = genVal < -16000.0f ? -16000.0f : genVal > 4500.0f ? 4500.0f : genVal;
                        viblfo_incr = (4.0f * FLUID_BUFSIZE * fluid_conv.fluid_act2hz(x) / output_rate);
                    }
                    break;

                case fluid_gen_type.GEN_VIBLFODELAY:
                    {
                        float x = genVal < -12000f ? -12000f : genVal > 5000f ? 5000f : genVal;
                        viblfo_delay = (uint)(output_rate * fluid_conv.fluid_tc2sec_delay(x));
                    }
                    break;

                case fluid_gen_type.GEN_VIBLFOTOPITCH:
                    viblfo_to_pitch = genVal < -12000f ? -12000f : genVal > 12000f ? 12000f : genVal;
                    break;

                case fluid_gen_type.GEN_KEYNUM:
                    {
                        // GEN_KEYNUM: SF2.01 page 46, item 46
                        // If this generator is active, it forces the key number to its value.  Non-realtime controller.
                        // There is a flag, which should indicate, whether a generator is enabled or not.  But here we rely on the default value of -1.
                        int x = Convert.ToInt32(genVal);
                        if (x >= 0) key = x;
                    }
                    break;

                case fluid_gen_type.GEN_VELOCITY:
                    {
                        // GEN_VELOCITY: SF2.01 page 46, item 47
                        // If this generator is active, it forces the velocity to its value. Non-realtime controller.
                        // There is a flag, which should indicate, whether a generator is enabled or not. But here we rely on the default value of -1. 
                        int x = Convert.ToInt32(genVal);
                        if (x >= 0) vel = x;
                    }
                    break;

                case fluid_gen_type.GEN_MODENVTOPITCH:
                    modenv_to_pitch = genVal < -12000.0f ? -12000.0f : genVal > 12000.0f ? 12000.0f : genVal;
                    break;

                case fluid_gen_type.GEN_MODENVTOFILTERFC:
                    // Range: SF2.01 section 8.1.3 # 1
                    // Motivation for range checking:Filter is reported to make funny noises now
                    break;

                // sample start and ends points
                // Range checking is initiated via the check_sample_sanity flag, because it is impossible to check here:
                // During the voice setup, all modulators are processed, while the voice is inactive. Therefore, illegal settings may
                // occur during the setup (for example: First move the loop end point ahead of the loop start point => invalid, then move the loop start point forward => valid again.
                // Unity adaptation: wave are played from a wave file not from a global data buffer. It's not possible de change these
                // value after importing the SoudFont. Only loop address are taken in account whrn importing the SF
                case fluid_gen_type.GEN_STARTADDROFS:              /* SF2.01 section 8.1.3 # 0 */
                case fluid_gen_type.GEN_STARTADDRCOARSEOFS:        /* SF2.01 section 8.1.3 # 4 */
                    if (sample != null)
                    {
                        start = (int)(sample.Start
                            + (int)gens[(int)fluid_gen_type.GEN_STARTADDROFS].Val + gens[(int)fluid_gen_type.GEN_STARTADDROFS].Mod /*+ gens[(int)fluid_gen_type.GEN_STARTADDROFS].nrpn*/
                            + 32768 * (int)gens[(int)fluid_gen_type.GEN_STARTADDRCOARSEOFS].Val + gens[(int)fluid_gen_type.GEN_STARTADDRCOARSEOFS].Mod /*+ gens[(int)fluid_gen_type.GEN_STARTADDRCOARSEOFS].nrpn*/);
                        if (start >= sample.Data.Length) start = sample.Data.Length - 1;
                        check_sample_sanity_flag = 1; //? FLUID_SAMPLESANITY_CHECK(1 << 0)
                    }
                    break;
                case fluid_gen_type.GEN_ENDADDROFS:                 /* SF2.01 section 8.1.3 # 1 */
                case fluid_gen_type.GEN_ENDADDRCOARSEOFS:           /* SF2.01 section 8.1.3 # 12 */
                    if (sample != null)
                    {
                        end = (int)(sample.End - 1
                            + (int)gens[(int)fluid_gen_type.GEN_ENDADDROFS].Val + gens[(int)fluid_gen_type.GEN_ENDADDROFS].Mod /*+ gens[(int)fluid_gen_type.GEN_ENDADDROFS].nrpn*/
                            + 32768 * (int)gens[(int)fluid_gen_type.GEN_ENDADDRCOARSEOFS].Val + gens[(int)fluid_gen_type.GEN_ENDADDRCOARSEOFS].Mod /*+ gens[(int)fluid_gen_type.GEN_ENDADDRCOARSEOFS].nrpn*/);
                        if (end >= sample.Data.Length) end = sample.Data.Length - 1;
                        check_sample_sanity_flag = 1; //? FLUID_SAMPLESANITY_CHECK(1 << 0)
                    }
                    break;
                case fluid_gen_type.GEN_STARTLOOPADDROFS:           /* SF2.01 section 8.1.3 # 2 */
                case fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS:     /* SF2.01 section 8.1.3 # 45 */
                    if (sample != null)
                    {
                        loopstart =
                            (int)(sample.LoopStart +
                            (int)gens[(int)fluid_gen_type.GEN_STARTLOOPADDROFS].Val +
                            gens[(int)fluid_gen_type.GEN_STARTLOOPADDROFS].Mod +
                            //gens[(int)fluid_gen_type.GEN_STARTLOOPADDROFS].nrpn +
                            32768 * (int)gens[(int)fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS].Val +
                            gens[(int)fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS].Mod /*+ gens[(int)fluid_gen_type.GEN_STARTLOOPADDRCOARSEOFS].nrpn*/);
                        if (loopstart >= sample.Data.Length) loopstart = sample.Data.Length - 1;
                        check_sample_sanity_flag = 1; //? FLUID_SAMPLESANITY_CHECK(1 << 0)
                    }
                    break;

                case fluid_gen_type.GEN_ENDLOOPADDROFS:             /* SF2.01 section 8.1.3 # 3 */
                case fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS:       /* SF2.01 section 8.1.3 # 50 */
                    if (sample != null)
                    {
                        loopend =
                            (int)(sample.LoopEnd +
                            (int)gens[(int)fluid_gen_type.GEN_ENDLOOPADDROFS].Val +
                            gens[(int)fluid_gen_type.GEN_ENDLOOPADDROFS].Mod
                            //+ gens[(int)fluid_gen_type.GEN_ENDLOOPADDROFS].nrpn
                            + 32768 * (int)gens[(int)fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS].Val +
                            gens[(int)fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS].Mod
                            /*+ gens[(int)fluid_gen_type.GEN_ENDLOOPADDRCOARSEOFS].nrpn*/);
                        if (loopend >= sample.Data.Length) loopend = sample.Data.Length - 1;
                        check_sample_sanity_flag = 1; //? FLUID_SAMPLESANITY_CHECK(1 << 0)
                    }
                    break;

                //
                // volume envelope
                //

                // - delay and hold times are converted to absolute number of samples
                // - sustain is converted to its absolute value
                // - attack, decay and release are converted to their increment per sample
                case fluid_gen_type.GEN_VOLENVDELAY:                /* SF2.01 section 8.1.3 # 33 */
                    {
                        float x = genVal < -12000f ? -12000f : genVal > 5000f ? 5000f : genVal;

                        uint count =
                            Convert.ToUInt32(output_rate * fluid_conv.fluid_tc2sec_delay(x) / FLUID_BUFSIZE);
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].count = count;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].coeff = 0f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].incr = 0f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].min = -1f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].max = 1f;
                    }
                    break;

                case fluid_gen_type.GEN_VOLENVATTACK:               /* SF2.01 section 8.1.3 # 34 */
                    {
                        float x = genVal < -12000f ? -12000f : genVal > 8000f ? 8000f : genVal;

                        uint count = 1 +
                                     Convert.ToUInt32(output_rate * fluid_conv.fluid_tc2sec_attack(x) / FLUID_BUFSIZE);

                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].count = count;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].coeff = 1f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].incr = count != 0 ? 1f / count : 0f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].min = -1f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].max = 1f;
                    }
                    break;

                case fluid_gen_type.GEN_VOLENVHOLD: /* SF2.01 section 8.1.3 # 35 */
                case fluid_gen_type.GEN_KEYTOVOLENVHOLD: /* SF2.01 section 8.1.3 # 39 */ {
                    uint count = calculate_hold_decay_buffers(fluid_gen_type.GEN_VOLENVHOLD,
                        fluid_gen_type.GEN_KEYTOVOLENVHOLD, false); /* 0 means: hold */

                    volenv_data[(int) fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].count = count;
                    volenv_data[(int) fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].coeff = 1f;
                    volenv_data[(int) fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].incr =
                        0f; // Volume stay stable during hold phase
                    volenv_data[(int) fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].min = -1f;
                    volenv_data[(int) fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].max = 2f; // was 1 with 2.05;
                }
                    break;

                case fluid_gen_type.GEN_VOLENVDECAY:               /* SF2.01 section 8.1.3 # 36 */
                case fluid_gen_type.GEN_VOLENVSUSTAIN:             /* SF2.01 section 8.1.3 # 37 */
                case fluid_gen_type.GEN_KEYTOVOLENVDECAY:          /* SF2.01 section 8.1.3 # 40 */
                    {
                        float y = 1f - 0.001f * CalculateGeneratorValue(fluid_gen_type.GEN_VOLENVSUSTAIN);
                        y = y < 0f ? 0f : y > 1f ? 1f : y;

                        uint count = calculate_hold_decay_buffers( fluid_gen_type.GEN_VOLENVDECAY,
                            fluid_gen_type.GEN_KEYTOVOLENVDECAY, true);
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].count = count;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].coeff = 1f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].incr = count != 0f ? -1f / count : 0f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].min = y; // Value to reach 
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].max = 2f;// was 1 with 2.05;
                    }
                    break;

                case fluid_gen_type.GEN_VOLENVRELEASE:             /* SF2.01 section 8.1.3 # 38 */
                    {
                        float x = genVal < FLUID_MIN_VOLENVRELEASE ? FLUID_MIN_VOLENVRELEASE : genVal > 8000.0f ? 8000.0f : genVal;
                        uint count = 1 + (uint)(output_rate * fluid_conv.fluid_tc2sec_release(x) * MPTK_ReleaseTimeMod / FLUID_BUFSIZE);

                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].count = count;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].coeff = 1f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].incr = count != 0 ? -1f / count : 0f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].min = 0f;
                        volenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].max = 1f;
                    }
                    break;

                //
                // Modulation envelope
                //
                // - delay and hold times are converted to absolute number of samples
                // - sustain is converted to its absolute value
                // - attack, decay and release are converted to their increment per sample
                case fluid_gen_type.GEN_MODENVDELAY:                /* SF2.01 section 8.1.3 # 33 */
                    {
                        float x = genVal < -12000f ? -12000f : genVal > 5000f ? 5000f : genVal;

                        uint count = Convert.ToUInt32(output_rate * fluid_conv.fluid_tc2sec_delay(x) / FLUID_BUFSIZE);
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].count = count;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].coeff = 0f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].incr = 0f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].min = -1f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY].max = 1f;
                    }
                    break;

                case fluid_gen_type.GEN_MODENVATTACK:               /* SF2.01 section 8.1.3 # 34 */
                    {
                        float x = genVal < -12000f ? -12000f : genVal > 8000f ? 8000f : genVal;

                        uint count = 1 +
                                     Convert.ToUInt32(output_rate * fluid_conv.fluid_tc2sec_attack(x) / FLUID_BUFSIZE);

                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].count = count;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].coeff = 1f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].incr = count != 0 ? 1f / count : 0f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].min = -1f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK].max = 1f;
                    }
                    break;

                case fluid_gen_type.GEN_MODENVHOLD: /* SF2.01 section 8.1.3 # 35 */
                case fluid_gen_type.GEN_KEYTOMODENVHOLD: /* SF2.01 section 8.1.3 # 39 */ {
                    uint count = calculate_hold_decay_buffers(fluid_gen_type.GEN_MODENVHOLD,
                        fluid_gen_type.GEN_KEYTOMODENVHOLD, false); /* 0 means: hold */

                    modenv_data[(int) fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].count = count;
                    modenv_data[(int) fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].coeff = 1f;
                    modenv_data[(int) fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].incr =
                        0f; // Volume stay stable during hold phase
                    modenv_data[(int) fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].min = -1f;
                    modenv_data[(int) fluid_voice_envelope_index.FLUID_VOICE_ENVHOLD].max = 2f; // was 1 with 2.05;
                }
                    break;

                case fluid_gen_type.GEN_MODENVDECAY:               /* SF2.01 section 8.1.3 # 36 */
                case fluid_gen_type.GEN_MODENVSUSTAIN:             /* SF2.01 section 8.1.3 # 37 */
                case fluid_gen_type.GEN_KEYTOMODENVDECAY:          /* SF2.01 section 8.1.3 # 40 */ {
                    uint count = calculate_hold_decay_buffers(fluid_gen_type.GEN_MODENVDECAY,
                        fluid_gen_type.GEN_KEYTOMODENVDECAY, true); /* 1 for decay */

                        float y = 1f - 0.001f * CalculateGeneratorValue(fluid_gen_type.GEN_MODENVSUSTAIN);
                        y = y < 0f ? 0f : y > 1f ? 1f : y;

                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].count = count;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].coeff = 1f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].incr = count != 0f ? -1f / count : 0f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].min = y; // Value to reach 
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVDECAY].max = 2f;// was 1 with 2.05;
                    }
                    break;

                case fluid_gen_type.GEN_MODENVRELEASE:             /* SF2.01 section 8.1.3 # 30 */
                    {
                        float x = genVal < -12000f ? -12000f : genVal > 8000.0f ? 8000.0f : genVal;
                        uint count = 1 + (uint)(output_rate * fluid_conv.fluid_tc2sec_release(x) / FLUID_BUFSIZE);

                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].count = count;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].coeff = 1f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].incr = count != 0 ? -1f / count : 0f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].min = 0f;
                        modenv_data[(int)fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE].max = 2f;
                    }
                    break;
            }
        }

        private float CalculateGeneratorValue(fluid_gen_type igen) {
            float genVal = gens[(int) igen].Val;
            return genVal + gens[(int) igen].Mod;
        }

        /*
         * calculate_hold_decay_buffers
         */
        uint calculate_hold_decay_buffers(fluid_gen_type gen_base, fluid_gen_type gen_key2base, bool is_decay) {
            /* Purpose:
             *
             * Returns the number of DSP loops, that correspond to the hold
             * (is_decay=0) or decay (is_decay=1) time.
             * gen_base=GEN_VOLENVHOLD, GEN_VOLENVDECAY, GEN_MODENVHOLD,
             * GEN_MODENVDECAY gen_key2base=GEN_KEYTOVOLENVHOLD,
             * GEN_KEYTOVOLENVDECAY, GEN_KEYTOMODENVHOLD, GEN_KEYTOMODENVDECAY
             */

            /* SF2.01 section 8.4.3 # 31, 32, 39, 40
             * GEN_KEYTOxxxENVxxx uses key 60 as 'origin'.
             * The unit of the generator is timecents per key number.
             * If KEYTOxxxENVxxx is 100, a key one octave over key 60 (72)
             * will cause (60-72)*100=-1200 timecents of time variation.
             * The time is cut in half.
             */
            float timecents = CalculateGeneratorValue(gen_base) + CalculateGeneratorValue(gen_key2base) * (60f - key);

            /* Range checking */
            if (is_decay) {
                /* SF 2.01 section 8.1.3 # 28, 36 */
                if (timecents > 8000f) {
                    timecents = 8000f;
                }
            } else {
                /* SF 2.01 section 8.1.3 # 27, 35 */
                if (timecents > 5000f) {
                    timecents = 5000f;
                }
                /* SF 2.01 section 8.1.2 # 27, 35:
                 * The most negative number indicates no hold time
                 */
                if (timecents <= -32768f) {
                    return 0;
                }
            }
            /* SF 2.01 section 8.1.3 # 27, 28, 35, 36 */
            if (timecents < -12000f) {
                timecents = -12000f;
            }

            float seconds = Mathf.Pow(2f, timecents / 1200f);
            /* Each DSP loop processes FLUID_BUFSIZE samples. */

            /* round to next full number of buffers */
            return (uint)((output_rate * seconds) / FLUID_BUFSIZE + 0.5f);
        }

        /**
         * fluid_voice_modulate
         *
         * In this implementation, I want to make sure that all controllers
         * are event based: the parameter values of the DSP algorithm should
         * only be updates when a controller event arrived and not at every
         * iteration of the audio cycle (which would probably be feasible if
         * the synth was made in silicon).
         *
         * The update is done in three steps:
         *
         * - first, we look for all the modulators that have the changed
         * controller as a source. This will yield a list of generators that
         * will be changed because of the controller event.
         *
         * - For every changed generator, calculate its new value. This is the
         * sum of its original value plus the values of al the attached
         * modulators.
         *
         * - For every changed generator, convert its value to the correct
         * unit of the corresponding DSP parameter
         *
         * @fn int fluid_voice_modulate(fluid_voice_t* voice, int cc, int ctrl, int val)
         * @param voice the synthesis voice
         * @param cc flag to distinguish between a continous control and a channel control (pitch bend, ...)
         * @param ctrl the control number
         * */
        public void fluid_voice_modulate(bool cc, int ctrl) {
            foreach (HiMod m in mods) {
                // step 1: find all the modulators that have the changed controller as input source.

                if (m.Src1 == ctrl && (m.Flags1 & (byte)fluid_mod_flags.FLUID_MOD_CC) != 0 && cc ||
                    m.Src1 == ctrl && (m.Flags1 & (byte)fluid_mod_flags.FLUID_MOD_CC) == 0 && !cc ||
                    m.Src2 == ctrl && (m.Flags2 & (byte)fluid_mod_flags.FLUID_MOD_CC) != 0 && cc ||
                    m.Src2 == ctrl && (m.Flags2 & (byte)fluid_mod_flags.FLUID_MOD_CC) == 0 && !cc)
                {

                    int igen = m.Dest; //fluid_mod_get_dest
                    float modval = 0.0f;

                    // step 2: for every changed modulator, calculate the modulation value of its associated generator
                    foreach (HiMod m1 in mods) {
                        if (m1.Dest == igen) //fluid_mod_has_dest(mod, gen)((mod).dest == gen)
                        {
                            modval += m1.fluid_mod_get_value(midiChannel, key, vel);
                        }
                    }

                    gens[igen].Mod = modval; //fluid_gen_set_mod(_gen, _val)  { (_gen).mod = (double)(_val); }

                    // step 3: now that we have the new value of the generator, recalculate the parameter values that are derived from the generator */
                    fluid_voice_update_param((fluid_gen_type) igen);
                }
            }
        }

        /// Update all the modulators. This function is called after a ALL_CTRL_OFF MIDI message has been received (CC 121).
        public void fluid_voice_modulate_all() {
            //Loop through all the modulators.
            //    FIXME: we should loop through the set of generators instead of the set of modulators. We risk to call 'fluid_voice_update_param'
            //    several times for the same generator if several modulators have that generator as destination. It's not an error, just a wast of
            //    energy (think polution, global warming, unhappy musicians, ...) 

            foreach (HiMod m in mods) {
                gens[m.Dest].Mod += m.fluid_mod_get_value(midiChannel, key, vel);
                int igen = m.Dest; //fluid_mod_get_dest
                float modval = 0.0f;
                // Accumulate the modulation values of all the modulators with destination generator 'gen'
                foreach (HiMod m1 in mods)
                {
                    if (m1.Dest == igen) //fluid_mod_has_dest(mod, gen)((mod).dest == gen)
                    {
                        modval += m1.fluid_mod_get_value(midiChannel, key, vel);
                    }
                }
                gens[igen].Mod = modval; //fluid_gen_set_mod(_gen, _val)  { (_gen).mod = (double)(_val); }

                // Update the parameter values that are depend on the generator 'gen'
                fluid_voice_update_param((fluid_gen_type) igen);
            }
        }

        /* Purpose:
         *
         * Make sure, that sample start / end point and loop points are in
         * proper order. When starting up, calculate the initial phase.
         */
        void fluid_voice_check_sample_sanity() {
            int min_index_nonloop = (int)sample.Start;
            int max_index_nonloop = (int)sample.End;

            /* make sure we have enough samples surrounding the loop */
            int min_index_loop = (int)sample.Start + FLUID_MIN_LOOP_PAD;
            int max_index_loop = (int)sample.End - FLUID_MIN_LOOP_PAD + 1;  /* 'end' is last valid sample, loopend can be + 1 */

            if (check_sample_sanity_flag == 0) {
                return;
            }

            /* Keep the start point within the sample data */
            if (start < min_index_nonloop) {
                start = min_index_nonloop;
            } else if (start > max_index_nonloop) {
                start = max_index_nonloop;
            }

            /* Keep the end point within the sample data */
            if (end < min_index_nonloop) {
                end = min_index_nonloop;
            } else if (end > max_index_nonloop) {
                end = max_index_nonloop;
            }

            /* Keep start and end point in the right order */
            if (start > end) {
                int temp = start;
                start = end;
                end = temp;
            }

            /* Zero length? */
            if (start == end) {
                fluid_voice_off();
                return;
            }


            if (IsLoop) {
                /* Keep the loop start point within the sample data */
                if (loopstart < min_index_loop) {
                    loopstart = min_index_loop;
                } else if (loopstart > max_index_loop) {
                    loopstart = max_index_loop;
                }

                /* Keep the loop end point within the sample data */
                if (loopend < min_index_loop) {
                    loopend = min_index_loop;
                } else if (loopend > max_index_loop) {
                    loopend = max_index_loop;
                }

                /* Keep loop start and end point in the right order */
                if (loopstart > loopend) {
                    int temp = loopstart;
                    loopstart = loopend;
                    loopend = temp;
                }

                /* Loop too short? Then don't loop. */
                if (loopend < loopstart + FLUID_MIN_LOOP_SIZE) {
                    gens[(int)fluid_gen_type.GEN_SAMPLEMODE].Val = (float)fluid_loop.FLUID_UNLOOPED;
                    IsLoop = false;
                }
            } /* if sample mode is looped */

            /* Run startup specific code (only once, when the voice is started)
#define FLUID_SAMPLESANITY_STARTUP (1 << 1) 
            */
            if ((check_sample_sanity_flag & 2) != 0)
            {
                if (max_index_loop - min_index_loop < FLUID_MIN_LOOP_SIZE)
                {
                    if ((byte)gens[(int)fluid_gen_type.GEN_SAMPLEMODE].Val == (byte)fluid_loop.FLUID_LOOP_UNTIL_RELEASE ||
                        (byte)gens[(int)fluid_gen_type.GEN_SAMPLEMODE].Val == (byte)fluid_loop.FLUID_LOOP_DURING_RELEASE)
                    {
                        gens[(int)fluid_gen_type.GEN_SAMPLEMODE].Val = (float)fluid_loop.FLUID_UNLOOPED;
                    }
                }

                // Set the initial phase of the voice (using the result from the start offset modulators). 
                //#define fluid_phase_set_int(a, b)    ((a) = ((unsigned long long)(b)) << 32)
                //fluid_phase_set_int(phase, start);
                phase = ((ulong)start) << 32;
            } /* if startup */

            // Is this voice run in loop mode, or does it run straight to the end of the waveform data?
            // (((_SAMPLEMODE(voice) == FLUID_LOOP_UNTIL_RELEASE) && (volenv_section < FLUID_VOICE_ENVRELEASE)) || (_SAMPLEMODE(voice) == FLUID_LOOP_DURING_RELEASE))

            if ((byte)gens[(int)fluid_gen_type.GEN_SAMPLEMODE].Val == (byte)fluid_loop.FLUID_LOOP_UNTIL_RELEASE &&
                volenv_section < fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE ||
                 (byte)gens[(int)fluid_gen_type.GEN_SAMPLEMODE].Val == (byte)fluid_loop.FLUID_LOOP_DURING_RELEASE)
            {
                /* Yes, it will loop as soon as it reaches the loop point.  In
                 * this case we must prevent, that the playback pointer (phase)
                 * happens to end up beyond the 2nd loop point, because the
                 * point has moved.  The DSP algorithm is unable to cope with
                 * that situation.  So if the phase is beyond the 2nd loop
                 * point, set it to the start of the loop. No way to avoid some
                 * noise here.  Note: If the sample pointer ends up -before the
                 * first loop point- instead, then the DSP loop will just play
                 * the sample, enter the loop and proceed as expected => no
                 * actions required.

                  Purpose: Return the index and the fractional part, respectively. 
#define fluid_phase_index(_x) ((uint)((_x) >> 32))
                  int index_in_sample = fluid_phase_index(phase);
                */

                int index_in_sample = (int)(phase >> 32);
                if (index_in_sample >= loopend) {
                    phase = ((ulong)loopstart) << 32;
                }
            }
            // Sample sanity has been assured. Don't check again, until some sample parameter is changed by modulation.
            check_sample_sanity_flag = 0;
        }

        /**
         * Call before fluid_voice_write (with positive argument),
         * and treat given amount of samples as zero samples of the delay.
         * When this starts returning 0, delay has elapsed and fluid_voice_write can be called.
         */
        public int fluid_voice_consume_delay(int maxDelaySamples) {
            int consumed = Math.Min(remainingDelaySamples, maxDelaySamples);
            remainingDelaySamples -= consumed;
            return consumed;
        }
        
        /*
         * fluid_voice_write - called from OnAudioFilterRead for each voices
         *
         * This is where it all happens. This function is called by the
         * synthesizer to generate the sound samples. The synthesizer passes
         * four audio buffers: left, right, reverb out, and chorus out.
         *
         * The biggest part of this function sets the correct values for all
         * the dsp parameters (all the control data boil down to only a few
         * dsp parameters). The dsp routine is #included in several places (fluid_dsp_core.c).
         */
        public int fluid_voice_write() {
            float target_amp;    /* target amplitude */

            uint FluidTicks = this.FluidTicks;
            this.FluidTicks += FLUID_BUFSIZE;

            Array.Clear(dsp_buf, 0, FLUID_BUFSIZE);

            /******************* sample **********************/

            /* Range checking for sample- and loop-related parameters
             * Initial phase is calculated here*/
            fluid_voice_check_sample_sanity();

            /******************* vol env **********************/

            fluid_env_data env_data = volenv_data[(int)volenv_section];

            /* skip to the next section of the envelope if necessary */
            while (volenv_count >= env_data.count)
            {
                volenv_section++;
                env_data = volenv_data[(int)volenv_section];
                volenv_count = 0;
            }


            /* calculate the envelope value and check for valid range */
            float x = env_data.coeff * volenv_val + env_data.incr;

            if (x < env_data.min) {
                x = env_data.min;
                volenv_section++;
                volenv_count = 0;
            } else if (x > env_data.max) {
                x = env_data.max;
                volenv_section++;
                volenv_count = 0;
            }

            volenv_val = x;
            volenv_count++;

            if (volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED) {
                fluid_voice_off();
                return 0;
            }

            /******************* mod env **********************/
            env_data = modenv_data[(int)modenv_section];

            /* skip to the next section of the envelope if necessary */
            while (modenv_count >= env_data.count)
            {
                modenv_section++;
                env_data = modenv_data[(int)modenv_section];
                modenv_count = 0;
            }

            /* calculate the envelope value and check for valid range */
            x = env_data.coeff * modenv_val + env_data.incr;

            if (x < env_data.min)
            {
                x = env_data.min;
                modenv_section++;
                modenv_count = 0;
            }
            else if (x > env_data.max)
            {
                x = env_data.max;
                modenv_section++;
                modenv_count = 0;
            }

            modenv_val = x;
            modenv_count++;

            /******************* mod lfo **********************/

            if (FluidTicks >= modlfo_delay) {
                modlfo_val += modlfo_incr;

                if (modlfo_val > 1f) {
                    modlfo_incr = -modlfo_incr;
                    modlfo_val = 2f - modlfo_val;
                } else if (modlfo_val < -1f) {
                    modlfo_incr = -modlfo_incr;
                    modlfo_val = -2f - modlfo_val;
                }
            }

            /******************* vib lfo **********************/

            if (FluidTicks >= viblfo_delay)
            {
                viblfo_val += viblfo_incr;

                if (viblfo_val > 1f) {
                    viblfo_incr = -viblfo_incr;
                    viblfo_val = 2f - viblfo_val;
                } else if (viblfo_val < -1f) {
                    viblfo_incr = -viblfo_incr;
                    viblfo_val = -2f - viblfo_val;
                }
            }

            /******************* amplitude **********************/

            /* calculate final amplitude
             * - initial gain
             * - amplitude envelope
             */

            if (volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVDELAY)
                return 0;  /* The volume amplitude is in hold phase. No sound is produced. */

            if (volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK) {
                /* the envelope is in the attack section: ramp linearly to max value.
                 * A positive modlfo_to_vol should increase volume (negative attenuation).
                 */
                target_amp = fluid_conv.fluid_atten2amp(attenuation) * fluid_conv.fluid_cb2amp(modlfo_val * -modlfo_to_vol) * volenv_val;
            } else {
                target_amp = fluid_conv.fluid_atten2amp(attenuation) * fluid_conv.fluid_cb2amp(960f * (1f - volenv_val) + modlfo_val * -modlfo_to_vol);

                /* We turn off a voice, if the volume has dropped low enough. */

                /* A voice can be turned off, when an estimate for the volume
                 * (upper bound) falls below that volume, that will drop the
                 * sample below the noise floor.
                 */

                /* attenuation_min is a lower boundary for the attenuation
                 * now and in the future (possibly 0 in the worst case).  Now the
                 * amplitude of sample and volenv cannot exceed amp_max (since
                 * volenv_val can only drop):
                 */

                float amp_max = fluid_conv.fluid_atten2amp(min_attenuation_cB) * volenv_val;

                /* And if amp_max is already smaller than the known amplitude,
                 * which will attenuate the sample below the noise floor, then we
                 * can safely turn off the voice. Duh. */
                if (amp_max <= MPTK_CutOffVolume) {
                    fluid_voice_off();
                    return 0;
                }
            }

            /* Volume increment to go from amp to target_amp in FLUID_BUFSIZE steps */
            amp_incr = (target_amp - amp) / FLUID_BUFSIZE;

            /* no volume and not changing? - No need to process */
            if (amp == 0f && amp_incr == 0f)
                return 0;

            /* Calculate the number of samples, that the DSP loop advances
             * through the original waveform with each step in the output
             * buffer. It is the ratio between the frequencies of original
             * waveform and output waveform.*/
            phase_incr = fluid_conv.fluid_ct2hz_real(
                pitch + modlfo_val * modlfo_to_pitch
                + viblfo_val * viblfo_to_pitch
                + modenv_val * modenv_to_pitch) / root_pitch;

            //fluid_check_fpe("voice_write phase calculation");

            /* if phase_incr is not advancing, set it to the minimum fraction value (prevent stuckage) */
            if (phase_incr == 0) phase_incr = 1;

            /*********************** run the dsp chain ************************
             * The sample is mixed with the output buffer.
             * The buffer has to be filled from 0 to FLUID_BUFSIZE-1.
             * Depending on the position in the loop and the loop size, this
             * may require several runs. */

            int sampleCount;
#pragma warning disable 162
            // ReSharper disable HeuristicUnreachableCode
            switch (InterpolationMethod) {
                case fluid_interp.None:
                    sampleCount = fluid_dsp_float.fluid_dsp_float_interpolate_none(this);
                    break;
                case fluid_interp.Linear:
                default:
                    sampleCount = fluid_dsp_float.fluid_dsp_float_interpolate_linear(this);
                    break;
                case fluid_interp.Cubic:
                    sampleCount = fluid_dsp_float.fluid_dsp_float_interpolate_4th_order(this);
                    break;
                case fluid_interp.Order7:
                    sampleCount = fluid_dsp_float.fluid_dsp_float_interpolate_7th_order(this);
                    break;
            }
            // ReSharper restore HeuristicUnreachableCode
#pragma warning restore 162

            /* turn off voice if short count (sample ended and not looping) */
            if (sampleCount < FLUID_BUFSIZE) {
                fluid_voice_off();
            } else if (remainingNoteSamples >= 0 && volenv_section != fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE) {
                remainingNoteSamples -= sampleCount;
                if (remainingNoteSamples <= 0) {
                    fluid_voice_noteoff();
                }
            }

            return sampleCount;
        }

        /// <summary>
        /// Move phase enveloppe to release
        /// </summary>
        public void fluid_voice_noteoff(bool force = false) {
            if (status != fluid_voice_status.FLUID_VOICE_ON && status != fluid_voice_status.FLUID_VOICE_SUSTAINED) return;
            
            if (!force && midiChannel != null && midiChannel.cc[(int)MPTKController.Sustain] >= 64) {
                status = fluid_voice_status.FLUID_VOICE_SUSTAINED;
            } else {
                if (volenv_section == fluid_voice_envelope_index.FLUID_VOICE_ENVATTACK) {
                    // A voice is turned off during the attack section of the volume envelope.
                    // The attack section ramps up linearly with amplitude.
                    // The other sections use logarithmic scaling.
                    // Calculate new volenv_val to achieve equivalent amplitude during the release phase for seamless volume transition.

                    if (volenv_val > 0) {
                        float lfo = modlfo_val * -modlfo_to_vol;
                        float vol = volenv_val * Mathf.Pow(10f, lfo / -200f);
                        float env_value = -((-200f * Mathf.Log(vol) / Mathf.Log(10f) - lfo) / 960f - 1f);
                        volenv_val = env_value > 1 ? 1 : env_value < 0 ? 0 : env_value;
                    }
                }
                volenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE;
                volenv_count = 0;

                modenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE;
                modenv_count = 0;
            }
        }


        /*
         * fluid_voice_kill_excl
         *
         * Percussion sounds can be mutually exclusive: for example, a 'closed
         * hihat' sound will terminate an 'open hihat' sound ringing at the
         * same time. This behaviour is modeled using 'exclusive classes',
         * turning on a voice with an exclusive class other than 0 will kill
         * all other voices having that exclusive class within the same preset
         * or channel.  fluid_voice_kill_excl gets called, when 'voice' is to
         * be killed for that reason.
         */
        public void fluid_voice_kill_excl() {
            if (status != fluid_voice_status.FLUID_VOICE_ON || status == fluid_voice_status.FLUID_VOICE_SUSTAINED) {
                return;
            }

            /* Turn off the exclusive class information for this voice, so that it doesn't get killed twice */
            //fluid_voice_gen_set(voice, GEN_EXCLUSIVECLASS, 0);
            gens[(int)fluid_gen_type.GEN_EXCLUSIVECLASS].Val = 0f;
            gens[(int)fluid_gen_type.GEN_EXCLUSIVECLASS].flags = fluid_gen_flags.GEN_SET_INSTRUMENT;

            /* If the voice is not yet in release state, put it into release state */
            if (volenv_section != fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE)
            {
                volenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE;
                volenv_count = 0;
                modenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE;
                modenv_count = 0;
            }

            // Speed up the volume envelope 
            // The value was found through listening tests with hi-hat samples. 
            //fluid_voice_gen_set(voice, GEN_VOLENVRELEASE, -200);
            gens[(int)fluid_gen_type.GEN_VOLENVRELEASE].Val = -200f;
            gens[(int)fluid_gen_type.GEN_VOLENVRELEASE].flags = fluid_gen_flags.GEN_SET_INSTRUMENT;
            fluid_voice_update_param(fluid_gen_type.GEN_VOLENVRELEASE);

            // Speed up the modulation envelope 
            //fluid_voice_gen_set(voice, GEN_MODENVRELEASE, -200);
            gens[(int)fluid_gen_type.GEN_MODENVRELEASE].Val = -200f;
            gens[(int)fluid_gen_type.GEN_MODENVRELEASE].flags = fluid_gen_flags.GEN_SET_INSTRUMENT;
            fluid_voice_update_param(fluid_gen_type.GEN_MODENVRELEASE);
        }

        /*
        * fluid_voice_off
        *
        * Purpose:
        * Turns off a voice, meaning that it is not processed
        * anymore by the DSP loop.
        */
        public void fluid_voice_off() {
            chan = NO_CHANNEL;
            volenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED;
            volenv_count = 0;
            modenv_section = fluid_voice_envelope_index.FLUID_VOICE_ENVFINISHED;
            modenv_count = 0;
            status = fluid_voice_status.FLUID_VOICE_OFF;
        }

    }
}
