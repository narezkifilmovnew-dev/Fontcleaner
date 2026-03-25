using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FontCleanerGUI
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private readonly Button cleanButton;
        private readonly TextBox logBox;
        private readonly CheckBox dryRunCheckBox;
        private readonly Label statusLabel;

        private readonly HashSet<string> safeFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Core UI / legacy-safe set
            "arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf",
            "calibri.ttf", "calibrib.ttf", "calibrii.ttf", "calibriz.ttf",
            "cambria.ttc", "cambriab.ttf", "cambriai.ttf", "cambriaz.ttf", "cambria.ttc",
            "consola.ttf", "consolab.ttf", "consolai.ttf", "consolaz.ttf",
            "cour.ttf", "courbd.ttf", "couri.ttf", "courbi.ttf",
            "georgia.ttf", "georgiab.ttf", "georgiai.ttf", "georgiaz.ttf",
            "impact.ttf",
            "lucon.ttf",
            "marlett.ttf",
            "micross.ttf",
            "msgothic.ttc", "msyh.ttc", "msyhbd.ttc", "msjhl.ttc",
            "ntailu.ttf", "nirmala.ttf", "nirmalab.ttf",
            "segmdl2.ttf", "seguihis.ttf", "seguibl.ttf", "seguibi.ttf", "seguibd.ttf", "segoepr.ttf", "segoeprb.ttf",
            "segoesc.ttf", "segoeui.ttf", "segoeuib.ttf", "segoeuii.ttf", "segoeuiz.ttf",
            "seguisym.ttf", "seguisb.ttf", "seguiemj.ttf", "seguisli.ttf", "segoeuil.ttf", "segoeuisl.ttf",
            "sitka.ttc", "sitkab.ttc", "sitkai.ttc", "sitkaz.ttc", "sitkas.ttc", "sitkasm.ttf", "sitkatt.ttf", "sitkati.ttf",
            "sylfaen.ttf",
            "symbol.ttf",
            "tahoma.ttf", "tahomabd.ttf",
            "times.ttf", "timesbd.ttf", "timesi.ttf", "timesbi.ttf",
            "trebuc.ttf", "trebucbd.ttf", "trebucit.ttf", "trebucbi.ttf",
            "verdana.ttf", "verdanab.ttf", "verdanai.ttf", "verdanaz.ttf",
            "webdings.ttf", "wingding.ttf", "wingding2.ttf", "wingding3.ttf"
        };

        public MainForm()
        {
            Text = "Font Cleaner GUI";
            Width = 860;
            Height = 560;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);

            var header = new Label
            {
                Text = "Удаление всех шрифтов кроме системных",
                Left = 16,
                Top = 16,
                Width = 600,
                Height = 28,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold)
            };

            var info = new Label
            {
                Text = "Рекомендуется запускать от имени администратора. По умолчанию включён безопасный режим предпросмотра.",
                Left = 16,
                Top = 50,
                Width = 800,
                Height = 36
            };

            dryRunCheckBox = new CheckBox
            {
                Left = 16,
                Top = 96,
                Width = 250,
                Height = 24,
                Checked = true,
                Text = "Без удаления (предпросмотр)"
            };

            cleanButton = new Button
            {
                Left = 280,
                Top = 92,
                Width = 180,
                Height = 34,
                Text = "Очистить шрифты"
            };
            cleanButton.Click += CleanButton_Click;

            statusLabel = new Label
            {
                Left = 16,
                Top = 132,
                Width = 800,
                Height = 24,
                Text = "Статус: готов"
            };

            logBox = new TextBox
            {
                Left = 16,
                Top = 164,
                Width = 810,
                Height = 340,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9F)
            };

            Controls.Add(header);
            Controls.Add(info);
            Controls.Add(dryRunCheckBox);
            Controls.Add(cleanButton);
            Controls.Add(statusLabel);
            Controls.Add(logBox);
        }

        private void CleanButton_Click(object sender, EventArgs e)
        {
            cleanButton.Enabled = false;
            try
            {
                bool dryRun = dryRunCheckBox.Checked;
                RunCleanup(dryRun);
            }
            catch (Exception ex)
            {
                Log("ОШИБКА: " + ex.Message);
                MessageBox.Show(ex.ToString(), "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                cleanButton.Enabled = true;
            }
        }

        private void RunCleanup(bool dryRun)
        {
            logBox.Clear();
            Log("Font Cleaner GUI started");
            Log("Mode: " + (dryRun ? "PREVIEW" : "DELETE"));
            Log("Admin: " + (IsAdministrator() ? "YES" : "NO"));

            if (!IsAdministrator())
            {
                MessageBox.Show(
                    "Запусти программу от имени администратора, иначе часть шрифтов и записей реестра не удалится.",
                    "Нужны права администратора",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            string fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            statusLabel.Text = "Статус: сканирование " + fontsDir;
            Log("Fonts dir: " + fontsDir);

            var files = Directory.GetFiles(fontsDir)
                .Where(f => IsFontExtension(Path.GetExtension(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int keepCount = 0;
            int deleteCount = 0;
            int errorCount = 0;

            foreach (var file in files)
            {
                string name = Path.GetFileName(file);
                if (safeFonts.Contains(name))
                {
                    keepCount++;
                    Log("KEEP    " + name);
                    continue;
                }

                if (dryRun)
                {
                    deleteCount++;
                    Log("DELETE? " + name);
                    continue;
                }

                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                    deleteCount++;
                    Log("DELETED " + name);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Log("FAILED  " + name + " -> " + ex.Message);
                }
            }

            using (var fontsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts", writable: !dryRun))
            {
                if (fontsKey != null)
                {
                    foreach (var valueName in fontsKey.GetValueNames())
                    {
                        var value = Convert.ToString(fontsKey.GetValue(valueName) ?? "");
                        var fileName = Path.GetFileName(value);
                        if (string.IsNullOrWhiteSpace(fileName))
                            continue;
                        if (safeFonts.Contains(fileName))
                            continue;

                        if (dryRun)
                        {
                            Log("REG DEL? " + valueName + " -> " + fileName);
                        }
                        else
                        {
                            try
                            {
                                fontsKey.DeleteValue(valueName, false);
                                Log("REG DEL  " + valueName + " -> " + fileName);
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                Log("REG FAIL " + valueName + " -> " + ex.Message);
                            }
                        }
                    }
                }
            }

            if (!dryRun)
            {
                TryRestartFontCache();
            }

            statusLabel.Text = "Статус: готово";
            Log("");
            Log("Done");
            Log("Kept:    " + keepCount);
            Log("Deleted: " + deleteCount);
            Log("Errors:  " + errorCount);

            MessageBox.Show(
                dryRun
                    ? "Предпросмотр завершён. Проверь лог и сними галочку для реального удаления."
                    : "Очистка завершена. Желательно перезагрузить Windows.",
                "Готово",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static bool IsFontExtension(string ext)
        {
            return string.Equals(ext, ".ttf", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".otf", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".ttc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".fon", StringComparison.OrdinalIgnoreCase);
        }

        private void TryRestartFontCache()
        {
            try
            {
                Log("Restarting Windows Font Cache service...");
                RunHidden("sc.exe", "stop FontCache");
                var cacheFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "FNTCACHE.DAT");
                if (File.Exists(cacheFile))
                {
                    File.SetAttributes(cacheFile, FileAttributes.Normal);
                    File.Delete(cacheFile);
                    Log("Deleted cache: " + cacheFile);
                }
                RunHidden("sc.exe", "start FontCache");
            }
            catch (Exception ex)
            {
                Log("Font cache restart failed: " + ex.Message);
            }
        }

        private static void RunHidden(string fileName, string args)
        {
            using (var p = new Process())
            {
                p.StartInfo.FileName = fileName;
                p.StartInfo.Arguments = args;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.Start();
                p.WaitForExit(15000);
            }
        }

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private void Log(string text)
        {
            logBox.AppendText(text + Environment.NewLine);
        }
    }
}
