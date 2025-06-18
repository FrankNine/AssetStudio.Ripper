namespace AssetStudioGUI;

using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using AssetRipper.SourceGenerated.Classes.ClassID_83;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.NativeEnums.Fmod;

using Logger = AssetStudio.Logger;

internal partial class AssetStudioGUIForm
{
    private FMOD.System system;
    private FMOD.Sound sound;
    private FMOD.Channel channel;
    private FMOD.SoundGroup masterSoundGroup;
    private FMOD.MODE loopMode = FMOD.MODE.LOOP_OFF;
    
    private uint FMODlenms;
    private uint FMODloopstartms;
    private uint FMODloopendms;
    private float FMODVolume = 0.8f;
    
    
    private string PreviewAudioClip(AssetItem assetItem, IAudioClip audioClip)
    {
        var sb = new StringBuilder();

        if (audioClip.Has_CompressionFormat())
        {
            sb.Append("Compression format: ");
            switch (audioClip.CompressionFormat)
            {
                case 0:
                    sb.AppendLine("PCM");
                    break;
                case 1:
                    sb.AppendLine("Vorbis");
                    break;
                case 2:
                    sb.AppendLine("ADPCM");
                    break;
                case 3:
                    sb.AppendLine("MP3");
                    break;
                case 4:
                    sb.AppendLine("VAG");
                    break;
                case 5:
                    sb.AppendLine("HEVAG");
                    break;
                case 6:
                    sb.AppendLine("XMA");
                    break;
                case 7:
                    sb.AppendLine("AAC");
                    break;
                case 8:
                    sb.AppendLine("GCADPCM");
                    break;
                case 9:
                    sb.AppendLine("ATRAC9");
                    break;
                default:
                    sb.AppendLine("Unknown");
                    break;
            }
        }
            
        if (audioClip.Has_Format())
        {
            sb.Append("Format: ");
            switch (audioClip.GetSoundFormat())
            {
                case FmodSoundFormat.None:
                    sb.AppendLine("None");
                    break;
                case FmodSoundFormat.Pcm8:
                    sb.AppendLine("PCM 8");
                    break;
                case FmodSoundFormat.Pcm16:
                    sb.AppendLine("PCM 16");
                    break;
                case FmodSoundFormat.Pcm24:
                    sb.AppendLine("PCM 24");
                    break;
                case FmodSoundFormat.Pcm32:
                    sb.AppendLine("PCM 32");
                    break;
                case FmodSoundFormat.Pcmfloat:
                    sb.AppendLine("PCM Float");
                    break;
                case FmodSoundFormat.Gcadpcm:
                    sb.AppendLine("Nintendo 3DS/Wii DSP GCADPCM");
                    break;
                case FmodSoundFormat.Imaadpcm:
                    sb.AppendLine("IMAADPCM"); 
                    break;
                case FmodSoundFormat.Vag:
                    sb.AppendLine("PlayStation 2 / PlayStation Portable ADPCM VAG");
                    break;
                case FmodSoundFormat.Hevag:
                    sb.AppendLine("PSVita ADPCM");
                    break;
                case FmodSoundFormat.Xma:
                    sb.AppendLine("Xbox 360 XMA");
                    break;
                case FmodSoundFormat.Mpeg:
                    sb.AppendLine("MP2/MP3 MPEG");
                    break;
                case FmodSoundFormat.Celt:
                    sb.AppendLine("CELT"); 
                    break;
                case FmodSoundFormat.At9:
                    sb.AppendLine("NGP ATRAC 9");
                    break;
                case FmodSoundFormat.Xwma:
                    sb.AppendLine("Xbox 360 XWMA");
                    break;
                case FmodSoundFormat.Vorbis:
                    sb.AppendLine("OGG Vorbis"); 
                    break;
                case FmodSoundFormat.Max:
                    sb.AppendLine("Max");  
                    break;
                case FmodSoundFormat.Forceint:
                    sb.AppendLine("Force Int");   
                    break;
                default:
                    sb.AppendLine( $"Unknown ({audioClip.GetSoundFormat()})");
                    break;
            }
        }

        if (audioClip.Has_Type())
        {
            sb.Append("Type: ");
            switch (audioClip.GetSoundType())
            {
                case FmodSoundType.Unknown:
                    sb.AppendLine("Unknown");
                    break;
                case FmodSoundType.Acc:
                    sb.AppendLine("ACC");
                    break;
                case FmodSoundType.Aiff:
                    sb.AppendLine("AIFF");
                    break;
                case FmodSoundType.Asf:
                    sb.AppendLine("ASF");
                    break;
                case FmodSoundType.At3:
                    sb.AppendLine("AT3");
                    break;
                case FmodSoundType.Cdda:
                    sb.AppendLine("CDDA");
                    break;
                case FmodSoundType.Dls:
                    sb.AppendLine("DLS");
                    break;
                case FmodSoundType.Flac:
                    sb.AppendLine("FLAC");
                    break;
                case FmodSoundType.Fsb:
                    sb.AppendLine("FSB");
                    break;
                case FmodSoundType.Gcadpcm:
                    sb.AppendLine("GCADPCM");
                    break;
                case FmodSoundType.It:
                    sb.AppendLine("IT");
                    break;
                case FmodSoundType.Midi:
                    sb.AppendLine("MIDI");
                    break;
                case FmodSoundType.Mod:
                    sb.AppendLine("MOD");
                    break;
                case FmodSoundType.Mpeg:
                    sb.AppendLine("MPEG");
                    break;
                case FmodSoundType.Oggvorbis:
                    sb.AppendLine("OGG Vorbis");
                    break;
                case FmodSoundType.Playlist:
                    sb.AppendLine("Playlist");
                    break;
                case FmodSoundType.Raw:
                    sb.AppendLine("Raw");
                    break;
                case FmodSoundType.S3m:
                    sb.AppendLine("S3M");
                    break;
                case FmodSoundType.Sf2:
                    sb.AppendLine("SF2");
                    break;
                case FmodSoundType.User:
                    sb.AppendLine("User");
                    break;
                case FmodSoundType.Wav:
                    sb.AppendLine("WAV");
                    break;
                case FmodSoundType.Xm:
                    sb.AppendLine("XM");
                    break;
                case FmodSoundType.Xma:
                    sb.AppendLine("XMA");
                    break;
                case FmodSoundType.Vag:
                    sb.AppendLine("VAG");
                    break;
                case FmodSoundType.Audioqueue:
                    sb.AppendLine("Audio Queue");
                    break;
                case FmodSoundType.Xwma:
                    sb.AppendLine("XWMA");
                    break;
                case FmodSoundType.Bcwav:
                    sb.AppendLine("BCWAV");
                    break;
                case FmodSoundType.At9:
                    sb.AppendLine("AT9");
                    break;
                case FmodSoundType.Vorbis:
                    sb.AppendLine("Vorbis");
                    break;
                case FmodSoundType.MediaFoundation:
                    sb.AppendLine("Media Foundation");
                    break;
                case FmodSoundType.Max:
                    sb.AppendLine("MAX");
                    break;
                case FmodSoundType.Forceint:
                    sb.AppendLine("Force Int");
                    break;
                default:
                    sb.AppendLine("Unknown");
                    break;
            }
        }

        if (audioClip.Has_Length())
            sb.AppendLine($"Length: {audioClip.Length:0.0##}");
        if (audioClip.Has_Channels())
            sb.AppendLine($"Channel count: {audioClip.Channels}");
        if(audioClip.Has_Frequency())
            sb.AppendLine($"Sample rate: {audioClip.Frequency}");
        if (audioClip.Has_BitsPerSample())
            sb.AppendLine($"Bit depth: {audioClip.BitsPerSample}");
            
        var audioData = audioClip.GetAudioData();
            
        if (audioData.Length == 0)
            return sb.ToString();
            
        var exinfo = new FMOD.CREATESOUNDEXINFO();

        exinfo.cbsize = Marshal.SizeOf(exinfo);
        exinfo.length = (uint)audioData.Length;

        var result = system.createSound(audioData, FMOD.MODE.OPENMEMORY | loopMode, ref exinfo, out sound);
        if (ERRCHECK(result)) return sb.ToString();

        sound.getNumSubSounds(out var numsubsounds);

        if (numsubsounds > 0)
        {
            result = sound.getSubSound(0, out var subSound);
            if (result == FMOD.RESULT.OK)
            {
                sound = subSound;
            }
        }

        result = sound.getLength(out FMODlenms, FMOD.TIMEUNIT.MS);
        if (ERRCHECK(result)) return sb.ToString();

        result = sound.getLoopPoints(out FMODloopstartms, FMOD.TIMEUNIT.MS, out FMODloopendms, FMOD.TIMEUNIT.MS);
        if (result == FMOD.RESULT.OK)
        {
            sb.AppendLine($"Loop Start: {(FMODloopstartms / 1000 / 60):00}:{(FMODloopstartms / 1000 % 60):00}.{(FMODloopstartms / 10 % 100):00}");
            sb.AppendLine($"Loop End: {(FMODloopendms / 1000 / 60):00}:{(FMODloopendms / 1000 % 60):00}.{(FMODloopendms / 10 % 100):00}");
        }
            
        _ = system.getMasterChannelGroup(out var channelGroup);
        result = system.playSound(sound, channelGroup, true, out channel);
        if (ERRCHECK(result)) return sb.ToString();

        FMODpanel.Visible = true;

        result = channel.getFrequency(out var frequency);
        if (ERRCHECK(result)) return sb.ToString();

        FMODinfoLabel.Text = frequency + " Hz";
        FMODtimerLabel.Text = $"00:00.00 / {(FMODlenms / 1000 / 60):00}:{(FMODlenms / 1000 % 60):00}.{(FMODlenms / 10 % 100):00}";

        return sb.ToString();
    }
        
    private void FMODinit()
    {
        FMODreset();

        var result = FMOD.Factory.System_Create(out system);
        if (ERRCHECK(result)) { return; }

        result = system.getVersion(out var version);
        ERRCHECK(result);
        if (version < FMOD.VERSION.number)
        {
            Logger.Error($"Error! You are using an old version of FMOD {version:X}. This program requires {FMOD.VERSION.number:X}.");
            Application.Exit();
        }

        result = system.init(2, FMOD.INITFLAGS.NORMAL, IntPtr.Zero);
        if (ERRCHECK(result)) { return; }

        result = system.getMasterSoundGroup(out masterSoundGroup);
        if (ERRCHECK(result)) { return; }

        result = masterSoundGroup.setVolume(FMODVolume);
        if (ERRCHECK(result)) { return; }
    }
        
    private bool ERRCHECK(FMOD.RESULT result)
    {
        if (result != FMOD.RESULT.OK)
        {
            FMODreset();
            Logger.Warning($"FMOD error! {result} - {FMOD.Error.String(result)}");
            return true;
        }
        return false;
    }

    private void FMODreset()
    {
        timer.Stop();
        FMODprogressBar.Value = 0;
        FMODtimerLabel.Text = "00:00.00 / 00:00.00";
        FMODstatusLabel.Text = "Stopped";
        FMODinfoLabel.Text = string.Empty;

        if (sound.hasHandle())
        {
            var result = sound.release();
            ERRCHECK(result);
            sound.clearHandle();
        }
    }
        
    private void FMODplayButton_Click(object sender, EventArgs e)
    {
        if (sound.hasHandle() && channel.hasHandle())
        {
            _ = system.getMasterChannelGroup(out var channelGroup);
            timer.Start();
            var result = channel.isPlaying(out var playing);
            if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
            {
                if (ERRCHECK(result)) { return; }
            }

            if (playing)
            {
                result = channel.stop();
                if (ERRCHECK(result)) { return; }

                result = system.playSound(sound, channelGroup, false, out channel);
                if (ERRCHECK(result)) { return; }

                FMODpauseButton.Text = "Pause";
            }
            else
            {
                result = system.playSound(sound, channelGroup, false, out channel);
                if (ERRCHECK(result)) { return; }
                FMODstatusLabel.Text = "Playing";

                if (FMODprogressBar.Value > 0)
                {
                    uint newms = FMODlenms / 1000 * (uint)FMODprogressBar.Value;

                    result = channel.setPosition(newms, FMOD.TIMEUNIT.MS);
                    if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                    {
                        if (ERRCHECK(result)) { return; }
                    }

                }
            }
        }
    }
        
    private void FMODpauseButton_Click(object sender, EventArgs e)
    {
        if (sound.hasHandle() && channel.hasHandle())
        {
            var result = channel.isPlaying(out var playing);
            if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
            {
                if (ERRCHECK(result)) { return; }
            }

            if (playing)
            {
                result = channel.getPaused(out var paused);
                if (ERRCHECK(result)) { return; }
                result = channel.setPaused(!paused);
                if (ERRCHECK(result)) { return; }

                if (paused)
                {
                    FMODstatusLabel.Text = "Playing";
                    FMODpauseButton.Text = "Pause";
                    timer.Start();
                }
                else
                {
                    FMODstatusLabel.Text = "Paused";
                    FMODpauseButton.Text = "Resume";
                    timer.Stop();
                }
            }
        }
    }

    private void FMODstopButton_Click(object sender, EventArgs e)
    {
        if (channel.hasHandle())
        {
            var result = channel.isPlaying(out var playing);
            if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
            {
                if (ERRCHECK(result)) { return; }
            }

            if (playing)
            {
                result = channel.stop();
                if (ERRCHECK(result)) { return; }
                //channel = null;
                //don't FMODreset, it will nullify the sound
                timer.Stop();
                FMODprogressBar.Value = 0;
                FMODtimerLabel.Text = "00:00.00 / 00:00.00";
                FMODstatusLabel.Text = "Stopped";
                FMODpauseButton.Text = "Pause";
            }
        }
    }

    private void FMODloopButton_CheckedChanged(object sender, EventArgs e)
    {
        FMOD.RESULT result;

        loopMode = FMODloopButton.Checked ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF;

        if (sound.hasHandle())
        {
            result = sound.setMode(loopMode);
            if (ERRCHECK(result)) { return; }
        }

        if (channel.hasHandle())
        {
            result = channel.isPlaying(out var playing);
            if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
            {
                if (ERRCHECK(result)) { return; }
            }

            result = channel.getPaused(out var paused);
            if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
            {
                if (ERRCHECK(result)) { return; }
            }

            if (playing || paused)
            {
                result = channel.setMode(loopMode);
                if (ERRCHECK(result)) { return; }
            }
        }
    }

    private void FMODvolumeBar_ValueChanged(object sender, EventArgs e)
    {
        FMODVolume = Convert.ToSingle(FMODvolumeBar.Value) / 10;

        var result = masterSoundGroup.setVolume(FMODVolume);
        if (ERRCHECK(result)) { return; }
    }

    private void FMODprogressBar_Scroll(object sender, EventArgs e)
    {
        if (channel.hasHandle())
        {
            uint newms = FMODlenms / 1000 * (uint)FMODprogressBar.Value;
            FMODtimerLabel.Text = $@"{newms / 1000 / 60:00}:{newms / 1000 % 60:00}.{newms / 10 % 100:00} / {FMODlenms / 1000 / 60:00}:{FMODlenms / 1000 % 60:00}.{FMODlenms / 10 % 100:00}";
        }
    }

    private void FMODprogressBar_MouseDown(object sender, MouseEventArgs e)
    {
        timer.Stop();
    }

    private void FMODprogressBar_MouseUp(object sender, MouseEventArgs e)
    {
        if (channel.hasHandle())
        {
            uint newms = FMODlenms / 1000 * (uint)FMODprogressBar.Value;

            var result = channel.setPosition(newms, FMOD.TIMEUNIT.MS);
            if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
            {
                if (ERRCHECK(result)) { return; }
            }


            result = channel.isPlaying(out var playing);
            if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
            {
                if (ERRCHECK(result)) { return; }
            }

            if (playing) { timer.Start(); }
        }
    }
        
    private void timer_Tick(object sender, EventArgs e)
    {
        uint ms = 0;
        bool playing = false;
        bool paused = false;

        if (channel.hasHandle())
        {
            var result = channel.getPosition(out ms, FMOD.TIMEUNIT.MS);
            if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
            {
                ERRCHECK(result);
            }

            result = channel.isPlaying(out playing);
            if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
            {
                ERRCHECK(result);
            }

            result = channel.getPaused(out paused);
            if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
            {
                ERRCHECK(result);
            }
        }

        FMODtimerLabel.Text = $@"{ms / 1000 / 60:00}:{ms / 1000 % 60:00}.{ms / 10 % 100:00} / {FMODlenms / 1000 / 60:00}:{FMODlenms / 1000 % 60:00}.{FMODlenms / 10 % 100:00}";
        FMODprogressBar.Value = (int)Math.Clamp(ms * 1000f / FMODlenms, 0, 1000);
        FMODstatusLabel.Text = paused ? "Paused " : playing ? "Playing" : "Stopped";

        if (system.hasHandle() && channel.hasHandle())
        {
            system.update();
        }
    } 
}