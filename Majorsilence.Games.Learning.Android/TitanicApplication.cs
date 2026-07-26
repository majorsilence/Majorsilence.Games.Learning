using Android.App;
using Android.Runtime;
using System;

namespace Majorsilence.Games.Learning.Android;

/// <summary>Only exists to host the [Application] attribute (the launcher icon/label) - SDLActivity/MainActivity does everything else.</summary>
[Application(Label = "Titanic", Icon = "@mipmap/appicon")]
public class TitanicApplication : Application
{
    public TitanicApplication(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership)
    {
    }
}
