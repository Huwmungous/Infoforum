using System.Collections.Concurrent;
using Concentus;
using Concentus.Enums;
using NAudio.Wave;

namespace ChitterChatterClient.Services;

/// <summary>
/// Service for audio capture and playback using NAudio with Opus encoding.
/// Supports push-to-talk and voice activity detection.
/// </summary>
public sealed class AudioService : IDisposable
{
    // Audio format: 48kHz mono 16-bit (standard for Opus)
    private const int SampleRate = 48000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;
    private const int FrameSizeMs = 20; // 20ms frames
    private const int FrameSamples = SampleRate * FrameSizeMs / 1000; // 960 samples per frame

    private WaveInEvent? _waveIn;
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _playbackBuffer;
    private IOpusEncoder? _encoder;
    private IOpusDecoder? _decoder;

    private bool _isCapturing;
    private bool _isPushToTalkActive;
    private bool _isMuted;
    private bool _isDeafened;
    private float _inputVolume = 1.0f;
    private float _outputVolume = 1.0f;

    // Voice activity detection
    private const float VoiceThreshold = 0.02f;
    private bool _isVoiceDetected;
    private DateTime _lastVoiceTime = DateTime.MinValue;
    private readonly TimeSpan _voiceHoldTime = TimeSpan.FromMilliseconds(300);

    // Jitter buffer for incoming audio
    private readonly ConcurrentDictionary<string, JitterBuffer> _jitterBuffers = new();

    public event Action<byte[]>? AudioCaptured;
    public event Action<bool>? SpeakingStateChanged;
    public event Action<string>? ErrorOccurred;
    public event Action<float>? InputLevelChanged;

    public bool IsCapturing => _isCapturing;
    public bool IsMuted => _isMuted;
    public bool IsDeafened => _isDeafened;
    public bool UsePushToTalk { get; set; }
    public bool IsPushToTalkActive => _isPushToTalkActive;

    public float InputVolume
    {
        get => _inputVolume;
        set => _inputVolume = Math.Clamp(value, 0f, 2f);
    }

    public float OutputVolume
    {
        get => _outputVolume;
        set => _outputVolume = Math.Clamp(value, 0f, 2f);
    }

    public void Initialise(int? inputDeviceIndex = null, int? outputDeviceIndex = null)
    {
        try
        {
            // Initialise Opus encoder/decoder using factory
            _encoder = OpusCodecFactory.CreateEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP);
            _encoder.Bitrate = 64000;
            _encoder.Complexity = 5;
            _encoder.UseVBR = true;

            _decoder = OpusCodecFactory.CreateDecoder(SampleRate, Channels);

            // Initialise audio capture
            _waveIn = new WaveInEvent
            {
                DeviceNumber = inputDeviceIndex ?? 0,
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = FrameSizeMs
            };
            _waveIn.DataAvailable += OnAudioDataAvailable;

            // Initialise audio playback
            _waveOut = new WaveOutEvent
            {
                DeviceNumber = outputDeviceIndex ?? 0
            };
            _playbackBuffer = new BufferedWaveProvider(new WaveFormat(SampleRate, BitsPerSample, Channels))
            {
                BufferDuration = TimeSpan.FromSeconds(2),
                DiscardOnBufferOverflow = true
            };
            _waveOut.Init(_playbackBuffer);
            _waveOut.Play();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Failed to initialise audio: {ex.Message}");
            throw;
        }
    }

    public void StartCapture()
    {
        if (_waveIn is null || _isCapturing) return;

        try
        {
            _waveIn.StartRecording();
            _isCapturing = true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Failed to start capture: {ex.Message}");
        }
    }

    public void StopCapture()
    {
        if (_waveIn is null || !_isCapturing) return;

        try
        {
            _waveIn.StopRecording();
            _isCapturing = false;

            if (_isVoiceDetected)
            {
                _isVoiceDetected = false;
                SpeakingStateChanged?.Invoke(false);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Failed to stop capture: {ex.Message}");
        }
    }

    public void SetPushToTalk(bool active)
    {
        _isPushToTalkActive = active;

        if (!UsePushToTalk) return;

        if (active && !_isVoiceDetected)
        {
            _isVoiceDetected = true;
            SpeakingStateChanged?.Invoke(true);
        }
        else if (!active && _isVoiceDetected)
        {
            _isVoiceDetected = false;
            SpeakingStateChanged?.Invoke(false);
        }
    }

    public void SetMuted(bool muted)
    {
        _isMuted = muted;

        if (muted && _isVoiceDetected)
        {
            _isVoiceDetected = false;
            SpeakingStateChanged?.Invoke(false);
        }
    }

    public void SetDeafened(bool deafened)
    {
        _isDeafened = deafened;

        // When deafened, also mute
        if (deafened)
        {
            SetMuted(true);
        }
    }

    public void PlayAudio(string userId, byte[] opusData)
    {
        if (_isDeafened || _decoder is null || _playbackBuffer is null) return;

        try
        {
            // Get or create jitter buffer for this user
            var jitterBuffer = _jitterBuffers.GetOrAdd(userId, _ => new JitterBuffer());

            // Decode Opus to PCM (uses short samples)
            var pcmBuffer = new short[FrameSamples];
            var samplesDecoded = _decoder.Decode(opusData.AsSpan(), pcmBuffer.AsSpan(), FrameSamples, false);

            if (samplesDecoded > 0)
            {
                // Apply output volume
                for (var i = 0; i < samplesDecoded; i++)
                {
                    pcmBuffer[i] = (short)Math.Clamp(pcmBuffer[i] * _outputVolume, short.MinValue, short.MaxValue);
                }

                // Convert to bytes
                var byteBuffer = new byte[samplesDecoded * 2];
                Buffer.BlockCopy(pcmBuffer, 0, byteBuffer, 0, byteBuffer.Length);

                // Add to playback buffer
                _playbackBuffer.AddSamples(byteBuffer, 0, byteBuffer.Length);
            }
        }
        catch (Exception ex)
        {
            // Log but don't throw - audio glitches shouldn't crash the app
            System.Diagnostics.Debug.WriteLine($"Audio decode error: {ex.Message}");
        }
    }

    private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_isMuted || _encoder is null) return;

        try
        {
            // Convert bytes to shorts
            var sampleCount = e.BytesRecorded / 2;
            var samples = new short[sampleCount];
            Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);

            // Calculate input level for visualisation
            var maxSample = samples.Max(s => Math.Abs(s));
            var level = maxSample / 32768f;
            InputLevelChanged?.Invoke(level);

            // Voice activity detection (if not using push-to-talk)
            if (!UsePushToTalk)
            {
                var isVoice = level > VoiceThreshold;

                if (isVoice)
                {
                    _lastVoiceTime = DateTime.UtcNow;

                    if (!_isVoiceDetected)
                    {
                        _isVoiceDetected = true;
                        SpeakingStateChanged?.Invoke(true);
                    }
                }
                else if (_isVoiceDetected && DateTime.UtcNow - _lastVoiceTime > _voiceHoldTime)
                {
                    _isVoiceDetected = false;
                    SpeakingStateChanged?.Invoke(false);
                }
            }

            // Only transmit if we should be speaking
            var shouldTransmit = UsePushToTalk ? _isPushToTalkActive : _isVoiceDetected;

            if (shouldTransmit)
            {
                // Apply input volume to samples
                for (var i = 0; i < sampleCount; i++)
                {
                    samples[i] = (short)Math.Clamp(samples[i] * _inputVolume, short.MinValue, short.MaxValue);
                }

                // Encode to Opus (uses short samples)
                var encodedBuffer = new byte[1275]; // Max Opus frame size
                var encodedLength = _encoder.Encode(samples.AsSpan(0, FrameSamples), FrameSamples, encodedBuffer.AsSpan(), encodedBuffer.Length);

                if (encodedLength > 0)
                {
                    var opusData = new byte[encodedLength];
                    Array.Copy(encodedBuffer, opusData, encodedLength);
                    AudioCaptured?.Invoke(opusData);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Audio capture error: {ex.Message}");
        }
    }

    public static IReadOnlyList<string> GetInputDevices()
    {
        var devices = new List<string>();
        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            devices.Add(caps.ProductName);
        }
        return devices;
    }

    public static IReadOnlyList<string> GetOutputDevices()
    {
        var devices = new List<string>();
        for (var i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            devices.Add(caps.ProductName);
        }
        return devices;
    }

    public void Dispose()
    {
        StopCapture();

        _waveIn?.Dispose();
        _waveOut?.Dispose();

        _waveIn = null;
        _waveOut = null;
        _playbackBuffer = null;
        _encoder = null;
        _decoder = null;
    }
}

/// <summary>
/// Simple jitter buffer for handling out-of-order and delayed packets.
/// </summary>
internal sealed class JitterBuffer
{
    private const int BufferSize = 10;
    private readonly Queue<byte[]> _buffer = new(BufferSize);
    private readonly object _lock = new();

    public void Add(byte[] data)
    {
        lock (_lock)
        {
            if (_buffer.Count >= BufferSize)
            {
                _buffer.Dequeue();
            }
            _buffer.Enqueue(data);
        }
    }

    public byte[]? Get()
    {
        lock (_lock)
        {
            return _buffer.Count > 0 ? _buffer.Dequeue() : null;
        }
    }
}
