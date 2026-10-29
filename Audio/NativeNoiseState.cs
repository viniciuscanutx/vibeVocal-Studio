using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VibeVocalStudio.Audio;

internal sealed class NativeNoiseState : SafeHandleZeroOrMinusOneIsInvalid
{
    private NativeNoiseState(nint state) : base(true) => SetHandle(state);

    public static NativeNoiseState Create()
    {
        if (GetFrameSize() != NoiseSuppressor.FrameSize)
            throw new InvalidOperationException("A versão da biblioteca de supressão de ruído é incompatível.");
        var state = CreateState(nint.Zero);
        if (state == nint.Zero)
            throw new InvalidOperationException("Não foi possível reservar memória para a supressão de ruído.");
        return new NativeNoiseState(state);
    }

    public void Process(float[] input, float[] output) => ProcessFrame(this, output, input);

    protected override bool ReleaseHandle()
    {
        DestroyState(handle);
        return true;
    }

    [DllImport("rnnoise", EntryPoint = "rnnoise_get_frame_size", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GetFrameSize();

    [DllImport("rnnoise", EntryPoint = "rnnoise_create", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern nint CreateState(nint model);

    [DllImport("rnnoise", EntryPoint = "rnnoise_destroy", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void DestroyState(nint state);

    [DllImport("rnnoise", EntryPoint = "rnnoise_process_frame", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern float ProcessFrame(NativeNoiseState state, [Out] float[] output, [In] float[] input);
}
