using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Windows;

namespace Shaiya_Duff_Updater
{
  public class App : Application
  {
    [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
    [DebuggerNonUserCode]
    public void InitializeComponent() => this.StartupUri = new Uri("SplashScreen.xaml", UriKind.Relative);

    // Carica DLL embedded (es. Ionic.Zip.dll) se non trovata su disco.
    // Registrato prima di qualsiasi init WPF per garantire disponibilità.
    private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
    {
      string dllName = new AssemblyName(args.Name).Name + ".dll";
      using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(dllName))
      {
        if (stream == null) return null;
        var bytes = new byte[stream.Length];
        stream.Read(bytes, 0, bytes.Length);
        return Assembly.Load(bytes);
      }
    }

    [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
    [STAThread]
    [DebuggerNonUserCode]
    public static void Main()
    {
      AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

      // .NET Framework 4.8 defaulta a TLS 1.0. Il server accetta solo TLS 1.2+.
      try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13; }
      catch { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; }

      App app = new App();
      app.InitializeComponent();
      app.Run();
    }
  }
}

