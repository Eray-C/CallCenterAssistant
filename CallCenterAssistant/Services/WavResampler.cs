using System;
using System.IO;

namespace CallCenterAssistant.Services
{
    public static class WavResampler
    {
        public static byte[] ResampleTo16KhzMono(byte[] inputWavBytes)
        {
            if (inputWavBytes == null || inputWavBytes.Length < 44)
                throw new ArgumentException("Geçersiz veya çok küçük WAV dosyası.");

            using var msInput = new MemoryStream(inputWavBytes);
            using var reader = new BinaryReader(msInput);

            // Read RIFF header
            string riff = new string(reader.ReadChars(4));
            if (riff != "RIFF")
                throw new ArgumentException("Geçersiz WAV dosyası: RIFF başlığı bulunamadı.");

            reader.ReadInt32(); // File size - 8
            string wave = new string(reader.ReadChars(4));
            if (wave != "WAVE")
                throw new ArgumentException("Geçersiz WAV dosyası: WAVE başlığı bulunamadı.");

            // Find fmt chunk
            string fmt = new string(reader.ReadChars(4));
            while (fmt != "fmt ")
            {
                int chunkSize = reader.ReadInt32();
                reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                if (reader.BaseStream.Position + 8 > reader.BaseStream.Length)
                    throw new ArgumentException("Geçersiz WAV dosyası: fmt chunk bulunamadı.");
                fmt = new string(reader.ReadChars(4));
            }

            int fmtChunkSize = reader.ReadInt32();
            short audioFormat = reader.ReadInt16(); // 1 for PCM
            short numChannels = reader.ReadInt16();
            int sampleRate = reader.ReadInt32();
            reader.ReadInt32(); // Byte rate
            short blockAlign = reader.ReadInt16();
            short bitsPerSample = reader.ReadInt16();

            // Skip remaining fmt chunk bytes if any
            if (fmtChunkSize > 16)
            {
                reader.BaseStream.Seek(fmtChunkSize - 16, SeekOrigin.Current);
            }

            // Find data chunk
            if (reader.BaseStream.Position + 8 > reader.BaseStream.Length)
                throw new ArgumentException("Geçersiz WAV dosyası: data chunk bulunamadı.");
            string dataHeader = new string(reader.ReadChars(4));
            while (dataHeader != "data")
            {
                int chunkSize = reader.ReadInt32();
                reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                if (reader.BaseStream.Position + 8 > reader.BaseStream.Length)
                    throw new ArgumentException("Geçersiz WAV dosyası: data chunk bulunamadı.");
                dataHeader = new string(reader.ReadChars(4));
            }

            int dataChunkSize = reader.ReadInt32();
            byte[] rawData = reader.ReadBytes(dataChunkSize);

            // If it's already 16kHz, mono, and 16-bit, return the original bytes
            if (sampleRate == 16000 && numChannels == 1 && bitsPerSample == 16 && audioFormat == 1)
            {
                return inputWavBytes;
            }

            if (audioFormat != 1)
                throw new ArgumentException("Yalnızca sıkıştırılmamış PCM WAV dosyaları desteklenmektedir.");

            if (bitsPerSample != 16)
                throw new ArgumentException("Yalnızca 16-bit WAV dosyaları desteklenmektedir.");

            // Convert raw bytes to 16-bit PCM samples
            int inputSampleCount = rawData.Length / (numChannels * 2);
            short[] inputSamples = new short[inputSampleCount];
            for (int i = 0; i < inputSampleCount; i++)
            {
                // Average the channels if multi-channel (e.g. stereo)
                int sum = 0;
                for (int c = 0; c < numChannels; c++)
                {
                    int index = (i * numChannels + c) * 2;
                    if (index + 1 < rawData.Length)
                    {
                        sum += BitConverter.ToInt16(rawData, index);
                    }
                }
                inputSamples[i] = (short)(sum / numChannels);
            }

            // Resample to 16000Hz using linear interpolation
            double ratio = 16000.0 / sampleRate;
            int outputSampleCount = (int)(inputSampleCount * ratio);
            short[] outputSamples = new short[outputSampleCount];

            for (int i = 0; i < outputSampleCount; i++)
            {
                double inputIndex = i / ratio;
                int indexLow = (int)Math.Floor(inputIndex);
                int indexHigh = (int)Math.Ceiling(inputIndex);

                if (indexHigh >= inputSamples.Length)
                {
                    indexHigh = inputSamples.Length - 1;
                }

                if (indexLow >= inputSamples.Length)
                {
                    indexLow = inputSamples.Length - 1;
                }

                double fraction = inputIndex - indexLow;
                
                // Linear interpolation
                outputSamples[i] = (short)((1 - fraction) * inputSamples[indexLow] + fraction * inputSamples[indexHigh]);
            }

            // Build new WAV file bytes
            using var msOutput = new MemoryStream();
            using var writerOutput = new BinaryWriter(msOutput);

            writerOutput.Write("RIFF".ToCharArray());
            writerOutput.Write(36 + outputSamples.Length * 2); // File size - 8
            writerOutput.Write("WAVE".ToCharArray());
            
            writerOutput.Write("fmt ".ToCharArray());
            writerOutput.Write(16); // Chunk size
            writerOutput.Write((short)1); // Audio format PCM
            writerOutput.Write((short)1); // Mono
            writerOutput.Write(16000); // 16kHz
            writerOutput.Write(16000 * 2); // Byte rate (SampleRate * NumChannels * BitsPerSample/8)
            writerOutput.Write((short)2); // Block align (NumChannels * BitsPerSample/8)
            writerOutput.Write((short)16); // 16-bit

            writerOutput.Write("data".ToCharArray());
            writerOutput.Write(outputSamples.Length * 2);
            for (int i = 0; i < outputSamples.Length; i++)
            {
                writerOutput.Write(outputSamples[i]);
            }

            return msOutput.ToArray();
        }
    }
}
