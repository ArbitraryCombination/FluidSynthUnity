namespace FluidSynth
{
    public enum fluid_gen_flags
    {
        GEN_UNUSED,     /**< Generator value is not set */
        GEN_SET_INSTRUMENT,        /**< Generator value is set at innstrument level*/
        GEN_SET_PRESET /**< DOCME */
    }

    /// <summary>
    /// Generator type. Value are defined in soundfont for each instruments and each note.
    /// This default value can be overriden in real time.
    /// See MPTKEvent class and https://paxstellar.fr/class-mptkevent#Generator-List
    /// </summary>
    public enum fluid_gen_type : byte {
        GEN_STARTADDROFS,       /**< Sample start address offset (0-32767) */
        GEN_ENDADDROFS,     /**< Sample end address offset (-32767-0) */
        GEN_STARTLOOPADDROFS,       /**< Sample loop start address offset (-32767-32767) */
        GEN_ENDLOOPADDROFS,     /**< Sample loop end address offset (-32767-32767) */
        GEN_STARTADDRCOARSEOFS, /**< Sample start address coarse offset (X 32768) */
        GEN_MODLFOTOPITCH,      /**< Modulation LFO to pitch */
        GEN_VIBLFOTOPITCH,      /**< Vibrato LFO to pitch */
        GEN_MODENVTOPITCH,      /**< Modulation envelope to pitch */
        GEN_FILTERFC,           /**< Filter cutoff */
        GEN_FILTERQ,            /**< Filter Q */
        GEN_MODLFOTOFILTERFC,       /**< Modulation LFO to filter cutoff */
        GEN_MODENVTOFILTERFC,       /**< Modulation envelope to filter cutoff */
        GEN_ENDADDRCOARSEOFS,       /**< Sample end address coarse offset (X 32768) 12 */
        GEN_MODLFOTOVOL,        /**< Modulation LFO to volume */
        GEN_UNUSED1,            /**< Unused */
        GEN_CHORUSSEND,     /**< Chorus send amount */
        GEN_REVERBSEND,     /**< Reverb send amount */
        GEN_PAN,            /**< Stereo panning */
        GEN_UNUSED2,            /**< Unused */
        GEN_UNUSED3,            /**< Unused */
        GEN_UNUSED4,            /**< Unused */
        GEN_MODLFODELAY,        /**< Modulation LFO delay */
        GEN_MODLFOFREQ,     /**< Modulation LFO frequency */
        GEN_VIBLFODELAY,        /**< Vibrato LFO delay */
        GEN_VIBLFOFREQ,     /**< Vibrato LFO frequency */
        GEN_MODENVDELAY,        /**< Modulation envelope delay */
        GEN_MODENVATTACK,       /**< Modulation envelope attack */
        GEN_MODENVHOLD,     /**< Modulation envelope hold */
        GEN_MODENVDECAY,        /**< Modulation envelope decay */
        GEN_MODENVSUSTAIN,      /**< Modulation envelope sustain */
        GEN_MODENVRELEASE,      /**< Modulation envelope release */
        GEN_KEYTOMODENVHOLD,        /**< Key to modulation envelope hold */
        GEN_KEYTOMODENVDECAY,       /**< Key to modulation envelope decay */
        GEN_VOLENVDELAY,        /**< Volume envelope delay */
        GEN_VOLENVATTACK,       /**< Volume envelope attack */
        GEN_VOLENVHOLD,     /**< Volume envelope hold */
        GEN_VOLENVDECAY,        /**< Volume envelope decay */
        GEN_VOLENVSUSTAIN,      /**< Volume envelope sustain */
        GEN_VOLENVRELEASE,      /**< Volume envelope release */
        GEN_KEYTOVOLENVHOLD,        /**< Key to volume envelope hold */
        GEN_KEYTOVOLENVDECAY,       /**< Key to volume envelope decay */
        GEN_INSTRUMENT,     /**< Instrument ID (shouldn't be set by user) */
        GEN_RESERVED1,      /**< Reserved */
        GEN_KEYRANGE,           /**< MIDI note range */
        GEN_VELRANGE,           /**< MIDI velocity range 44 */
        GEN_STARTLOOPADDRCOARSEOFS, /**< Sample start loop address coarse offset (X 32768) */
        GEN_KEYNUM,         /**< Fixed MIDI note number */
        GEN_VELOCITY,           /**< Fixed MIDI velocity value */
        GEN_ATTENUATION,        /**< Initial volume attenuation 48 */
        GEN_RESERVED2,      /**< Reserved */
        GEN_ENDLOOPADDRCOARSEOFS,   /**< Sample end loop address coarse offset (X 32768) */
        GEN_COARSETUNE,     /**< Coarse tuning */
        GEN_FINETUNE,           /**< Fine tuning */
        GEN_SAMPLEID,           /**< Sample ID (shouldn't be set by user) */
        GEN_SAMPLEMODE,     /**< Sample mode flags 54 */
        GEN_RESERVED3,      /**< Reserved */
        GEN_SCALETUNE,      /**< Scale tuning */
        GEN_EXCLUSIVECLASS,     /**< Exclusive class number */
        GEN_OVERRIDEROOTKEY,        /**< Sample root note override 58 */

        // the initial pitch is not a "standard" generator. It is not mentioned in the list of generator in the SF2 specifications.
        // It is used, however, as the destination for the default pitch wheel modulator.
        GEN_PITCH,          /**< Pitch (NOTE: Not a real SoundFont generator) */

        GEN_LAST // Sentinel
    }

    /// <summary>
    /// Defined generator from fluid_gen_t
    /// </summary>
    public class HiGen {
        public fluid_gen_type type;
        public fluid_gen_flags flags; /**< Is the generator set or not (#fluid_gen_flags) */
        public float Val;          /**< The nominal value */
        public float Mod;          /**< Change by modulators */

        // generator value
        public short Amount;

        public byte AmountLo => (byte) (Amount & 0xFF);
        public byte AmountHi => (byte) ((Amount >> 8) & 0xFF);

        public override string ToString() {
            return $"Gen {type} flags:{flags} val:{Val} mod:{Mod}";
        }
    }
}
