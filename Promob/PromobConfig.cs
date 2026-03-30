using System;
using System.IO;

namespace AutomacaoPromobTeste.Promob{
    public static class PromobConfig{
        // Pastas
        public static readonly string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        public static readonly string PastaPromob = Path.Combine(DesktopPath, "promob");
        public static readonly string PastaXml = Path.Combine(DesktopPath, "xml");

        // Timeouts / intervalos
        public const int TimeoutCurto = 2000;
        public const int TimeoutPadrao = 5000;
        public const int TimeoutLongo = 10000;
        public const int PollMs = 200;
        public const int DelayMinimo = 150;

        // Seletores / textos
        public const string NomeJanelaWizardImportacao = "Importar projeto";
        public const string AutomationIdImportar = "ProjectImport";
        public const string AutomationIdHost = "elementHost1";
    }
}
