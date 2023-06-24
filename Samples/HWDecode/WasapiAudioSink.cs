using FFmpeg.Wrapper;
using System.Runtime.Versioning;

using Windows.Win32.Media.Audio;
using Windows.Win32.System.Com;

using static Windows.Win32.PInvoke;

[SupportedOSPlatform("windows10.0")]
public unsafe class WasapiAudioSink : IAudioSink
{
    IAudioClient* _client;
    IAudioRenderClient* _renderer;
    AutoResetEvent _event;

    public AudioFormat Format { get; }
    readonly int _bytesPerFrame;

    public WasapiAudioSink(AudioFormat preferredFormat, int latencyMs)
    {
        IMMDevice* device = null;
        WAVEFORMATEX* actualFmt = null;

        CoCreateInstance(typeof(MMDeviceEnumerator).GUID, null, CLSCTX.CLSCTX_INPROC_SERVER, out IMMDeviceEnumerator* devEnumer).ThrowOnFailure();
        try {
            devEnumer->GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, &device);

            IAudioClient* client;
            IAudioRenderClient* renderer;

            device->Activate(IAudioClient.IID_Guid, CLSCTX.CLSCTX_INPROC_SERVER, null, out *(void**)&client);

            Format = PickFormat(client, preferredFormat, out actualFmt);
            _bytesPerFrame = actualFmt->nBlockAlign;

            client->Initialize(
                AUDCLNT_SHAREMODE.AUDCLNT_SHAREMODE_SHARED,
                AUDCLNT_STREAMFLAGS_EVENTCALLBACK | AUDCLNT_STREAMFLAGS_NOPERSIST,
                latencyMs * 10_000, 0, actualFmt, null);

            _event = new AutoResetEvent(false);
            client->SetEventHandle(_event.SafeWaitHandle);

            client->GetService(IAudioRenderClient.IID_Guid, out *(void**)&renderer);

            _client = client;
            _renderer = renderer;
        } finally {
            CoTaskMemFree(actualFmt);
            if (device != null) device->Release();
            devEnumer->Release();
        }
    }

    static readonly Guid
        KSDATAFORMAT_SUBTYPE_IEEE_FLOAT = Guid.Parse("00000003-0000-0010-8000-00AA00389B71"),
        KSDATAFORMAT_SUBTYPE_PCM = Guid.Parse("00000001-0000-0010-8000-00AA00389B71");

    private static AudioFormat PickFormat(IAudioClient* client, AudioFormat preferredFmt, out WAVEFORMATEX* actualFmt)
    {
        bool preferFloat = preferredFmt.SampleFormat is SampleFormats.S16 or SampleFormats.S16Planar;
        WAVEFORMATEX probeFmt = new() {
            nSamplesPerSec = (uint)preferredFmt.SampleRate,
            nChannels = (ushort)preferredFmt.NumChannels,
            wFormatTag = (ushort)(preferFloat ? WAVE_FORMAT_IEEE_FLOAT : WAVE_FORMAT_PCM),
            wBitsPerSample = (ushort)(preferFloat ? 32 : 16)
        };
        probeFmt.nBlockAlign = (ushort)((probeFmt.nChannels * probeFmt.wBitsPerSample) / 8);
        probeFmt.nAvgBytesPerSec = probeFmt.nBlockAlign * probeFmt.nSamplesPerSec;

        var result = client->IsFormatSupported(AUDCLNT_SHAREMODE.AUDCLNT_SHAREMODE_SHARED, &probeFmt, null);
        
        if (result == 0) {
            actualFmt = (WAVEFORMATEX*)CoTaskMemAlloc((nuint)sizeof(WAVEFORMATEX));
            *actualFmt = probeFmt;
        } else {
            client->GetMixFormat(out actualFmt);
        }

        var wex = (WAVEFORMATEXTENSIBLE*)actualFmt;

        var sampleFmt = (uint)actualFmt->wFormatTag switch {
            WAVE_FORMAT_PCM => SampleFormats.S16,
            WAVE_FORMAT_IEEE_FLOAT => SampleFormats.Float,
            WAVE_FORMAT_EXTENSIBLE when wex->SubFormat == KSDATAFORMAT_SUBTYPE_PCM => SampleFormats.S16,
            WAVE_FORMAT_EXTENSIBLE when wex->SubFormat == KSDATAFORMAT_SUBTYPE_IEEE_FLOAT => SampleFormats.Float,
        };
        //TODO: consider parsing channel layout
        return new AudioFormat(sampleFmt, (int)actualFmt->nSamplesPerSec, actualFmt->nChannels);
    }

    public void Start()
    {
        _client->Start();
    }
    public void Stop()
    {
        _client->Stop();
    }

    public Span<T> GetQueueBuffer<T>() where T : unmanaged
    {
        _client->GetBufferSize(out uint maxFrames);
        _client->GetCurrentPadding(out uint numPadFrames);

        uint availFrames = maxFrames - numPadFrames;
        _renderer->GetBuffer(availFrames, out byte* buffer);

        return new Span<T>(buffer, (int)(availFrames * _bytesPerFrame / sizeof(T)));
    }
    
    public void AdvanceQueue(int numFramesWritten, bool replaceWithSilence = false)
    {
        _renderer->ReleaseBuffer((uint)numFramesWritten, replaceWithSilence ? 0x2u : 0u);
    }

    public void Wait()
    {
        _event.WaitOne();
    }

    public TimeSpan GetLatency()
    {
        _client->GetStreamLatency(out long ticks);
        return TimeSpan.FromTicks(ticks);
    }

    public void Dispose()
    {
        if (_client != null) {
            _renderer->Release();
            _client->Release();

            _renderer = null;
            _client = null;

            _event.Dispose();
            GC.SuppressFinalize(this);
        }
    }
    ~WasapiAudioSink() { Dispose(); }
}
