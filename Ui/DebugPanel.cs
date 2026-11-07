/*
 * Arise - RTM (GTA V 1.57) - FR
 * Copyright (C) 2026 yAmethxst — code d'origine
 * Copyright (C) 2026 Zitoune — modifications du fork
 *
 * Ce fichier est distribué sous GNU GPL v3 (voir LICENSE). Les parties issues
 * de l'outil d'origine restent sous licence MIT (voir LICENSE-MIT).
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Arise_RTM
{
    /// <summary>
    /// Onglet Debug pour utilisateurs avancés : écrire ou lire une stat par nom, fixer une valeur
    /// packed par index, et exécuter une liste d'écritures depuis un fichier texte sur le PC.
    /// Rien n'est bundlé : l'outil n'écrit que ce que vous tapez ou chargez.
    /// </summary>
    public sealed class DebugPanel : Panel
    {
        private const int ColumnWidth = 470;
        private const int RightColumnX = 500;
        private const int WideWidth = 956;

        private static readonly string[] PrefixChoices = { "Stat personnage", "Stat compte", "Nom exact" };
        private static readonly Regex AlreadyPrefixed = new Regex(@"^(MP\d_|MPPLY_)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private const string Intro =
            "Outils avancés. Écrivez ou lisez une stat, fixez une valeur packed, ou exécutez une liste d'écritures. Tout va directement au jeu, donc vérifiez ce que vous tapez.";

        private const string CharacterHint =
            "Vos deux personnages s'appellent MP0_ (Personnage 1) et MP1_ (Personnage 2) dans le jeu. " +
            "C'est le même choix que « Personnage » dans la barre d'outils.";

        private const string PrefixHint =
            "Stat personnage : l'outil ajoute MP0_ ou MP1_ à votre place. Stat compte : il ajoute MPPLY_. Nom exact : il écrit le nom tel que tapé.";

        private const string TypeHint =
            "Choisissez le type qui correspond à la stat : int = entier, bool = true / false, float = nombre décimal.";

        private const string PackedHint =
            "Une valeur packed est un drapeau (ou un petit nombre) stocké par numéro (index) plutôt que par nom. Elle appartient au personnage choisi.";

        private const string ListFormat =
            "int    MP_CHAR_ABILITY_1_UNLCK   -1       stat, entier 32 bits\r\n" +
            "bool   MP_SOME_STAT              true     true / false (ou 1 / 0)\r\n" +
            "float  MPPLY_SOME_STAT           6.66     nombre décimal\r\n" +
            "pbool  1234                      true     drapeau packed par index\r\n" +
            "pint   1234                      5        entier packed par index";

        private const string ListNotes =
            "Une écriture par ligne, # démarre un commentaire. MP_ au début d'un nom = le personnage choisi ci-dessus ; " +
            "tout autre nom est utilisé tel que tapé. Voir stats_example.txt. Les options de la barre d'outils " +
            "(Enregistrer après écriture, Vérifier la relecture, Demander avant d'appliquer) s'appliquent aussi aux listes, " +
            "et Annuler stoppe une longue liste : ce qui est déjà écrit reste écrit.";

        private const string ListWarning =
            "L'outil écrit exactement ce que dit le fichier, sans contrôle sur les valeurs. Une liste erronée peut " +
            "abîmer votre personnage ou votre compte : utilisez des listes de sources de confiance, et changez de session après.";

        private readonly IToolHost host;
        private readonly List<TileButton> actionButtons = new List<TileButton>();
        private readonly ChoiceBar characterBar;
        private readonly ChoiceBar setPrefix, getPrefix, packedValue;
        private readonly TextBox setName, setValue, getName, result;
        private readonly Label setFullName, getFullName;
        private readonly Label introLabel, characterHint, packedHint, packedIndexLabel;
        private readonly Label setHint, getHint;
        private readonly Label listIntro, listFormat, listNotes, listWarning;
        private readonly TileButton checkListButton, runListButton;
        private readonly NumberBox packedIndex;
        private Section characterSection, setSection, getSection, packedSection, listSection;
        private bool syncing;

        public DebugPanel(IToolHost host)
        {
            this.host = host;
            Text = "Debug";

            introLabel = new Label { Text = Intro, ForeColor = UiStyle.Muted };
            Controls.Add(introLabel);

            // ---- personnage
            characterSection = new Section { Text = "Personnage", Location = new Point(14, 30), Size = new Size(WideWidth, 60) };
            characterBar = new ChoiceBar(new[] { "Personnage 1  (MP0_)", "Personnage 2  (MP1_)" }, 190, 26, -1) { Location = new Point(14, 26) };
            characterBar.SelectedChanged += (s, e) =>
            {
                if (!syncing && characterBar.SelectedIndex >= 0)
                    host.ChooseCharacter(characterBar.SelectedIndex);
            };
            characterSection.Controls.Add(characterBar);
            characterHint = new Label { Text = CharacterHint, ForeColor = UiStyle.Muted };
            characterSection.Controls.Add(characterHint);
            Controls.Add(characterSection);

            // ---- écrire stat
            setSection = new Section { Text = "Écrire une stat", Location = new Point(14, 98), Size = new Size(ColumnWidth, 208) };
            setPrefix = new ChoiceBar(PrefixChoices, 148, 24, 0) { Location = new Point(14, 26) };
            setSection.Controls.Add(setPrefix);
            setName = TextField(setSection, "Nom", 14, 58, 440, "ex. CHAR_ABILITY_1_UNLCK");
            setValue = TextField(setSection, "Valeur", 14, 86, 440, "ex. -1, true ou 6.66");
            setFullName = FullNameLabel(setSection, 14, 112);
            AddButtons(setSection, 14, 134, ("Écrire int", () => SetStat("int")), ("Écrire bool", () => SetStat("bool")), ("Écrire float", () => SetStat("float")));
            setHint = new Label { Text = TypeHint, Location = new Point(14, 166), Size = new Size(440, 32), ForeColor = UiStyle.Muted };
            setSection.Controls.Add(setHint);
            Controls.Add(setSection);

            // ---- lire stat
            getSection = new Section { Text = "Lire une stat", Location = new Point(RightColumnX, 98), Size = new Size(ColumnWidth, 208) };
            getPrefix = new ChoiceBar(PrefixChoices, 148, 24, 0) { Location = new Point(14, 26) };
            getSection.Controls.Add(getPrefix);
            getName = TextField(getSection, "Nom", 14, 58, 440, "ex. CHAR_ABILITY_1_UNLCK");
            getFullName = FullNameLabel(getSection, 14, 82);
            AddButtons(getSection, 14, 106, ("Lire int", () => GetStat("int")), ("Lire bool", () => GetStat("bool")), ("Lire float", () => GetStat("float")));
            getSection.Controls.Add(new Label { Text = "Résultat", Location = new Point(14, 146), Size = new Size(60, 16), ForeColor = Theme.Muted });
            result = new TextBox { Location = new Point(76, 142), Size = new Size(378, 23), ReadOnly = true };
            getSection.Controls.Add(result);
            getHint = new Label { Text = PrefixHint, Location = new Point(14, 172), Size = new Size(440, 32), ForeColor = UiStyle.Muted };
            getSection.Controls.Add(getHint);
            Controls.Add(getSection);

            // ---- packed
            packedSection = new Section { Text = "Valeur packed", Location = new Point(14, 314), Size = new Size(WideWidth, 88) };
            packedHint = new Label { Text = PackedHint, ForeColor = UiStyle.Muted };
            packedSection.Controls.Add(packedHint);
            packedIndexLabel = new Label { Text = "Indice", AutoSize = true };
            packedSection.Controls.Add(packedIndexLabel);
            packedIndex = new NumberBox { Minimum = 0, Maximum = 200000, Width = 90 };
            packedSection.Controls.Add(packedIndex);
            packedValue = new ChoiceBar(new[] { "true", "false" }, 70, 24, 0);
            packedSection.Controls.Add(packedValue);
            AddButtons(packedSection, 326, 48, ("Écrire", () => SetPacked()), ("Lire", () => GetPacked()));
            Controls.Add(packedSection);

            // ---- liste
            listSection = new Section { Text = "Exécuter une liste depuis un fichier", Location = new Point(14, 410), Size = new Size(WideWidth, 248) };
            listIntro = NewFlowLabel("Chargez une liste depuis votre PC (fichier .txt). Format, une écriture par ligne :", UiStyle.Muted, null);
            listFormat = NewFlowLabel(ListFormat, Theme.Text, Theme.Mono);
            listNotes = NewFlowLabel(ListNotes, UiStyle.Muted, null);
            listWarning = NewFlowLabel(ListWarning, Theme.Yellow, null);
            listSection.Controls.Add(listIntro);
            listSection.Controls.Add(listFormat);
            listSection.Controls.Add(listNotes);
            listSection.Controls.Add(listWarning);

            checkListButton = new TileButton { Text = "Vérifier une liste (n'écrit rien)", Outline = true, Centered = true };
            checkListButton.Click += (s, e) => CheckList();
            listSection.Controls.Add(checkListButton);

            runListButton = new TileButton { Text = "Exécuter une liste...", Primary = true, Centered = true };
            runListButton.Click += async (s, e) => await RunList();
            actionButtons.Add(runListButton);
            listSection.Controls.Add(runListButton);
            Controls.Add(listSection);

            setPrefix.SelectedChanged += (s, e) => RefreshFullNames();
            getPrefix.SelectedChanged += (s, e) => RefreshFullNames();
            setName.TextChanged += (s, e) => RefreshFullNames();
            getName.TextChanged += (s, e) => RefreshFullNames();
            Resize += (s, e) => LayoutAdaptive();
            SetCharacter(host.Slot);
            LayoutAdaptive();
        }

        /// <summary>Deux colonnes tant que la largeur le permet, une seule ensuite.
        /// Tous les textes suivent la largeur disponible et la page se réempile verticalement.</summary>
        private void LayoutAdaptive()
        {
            if (setSection == null || getSection == null || packedSection == null || listSection == null)
                return;

            int width = Math.Max(320, ClientSize.Width - 28);
            bool twoColumns = width >= 940;
            int columnWidth = twoColumns ? Math.Max(320, (width - 30) / 2) : width;
            int contentWidth = Math.Max(120, width - 28);

            int y = 8;
            introLabel.SetBounds(14, y, width, TextHeight(introLabel, width));
            y = introLabel.Bottom + 6;

            characterSection.SetBounds(14, y, width, 60);
            if (twoColumns)
            {
                characterHint.SetBounds(404, 26, Math.Max(120, contentWidth - 390), TextHeight(characterHint, contentWidth - 390));
                characterSection.Height = Math.Max(60, 26 + characterHint.Height + 8);
            }
            else
            {
                characterHint.SetBounds(14, 60, contentWidth, TextHeight(characterHint, contentWidth));
                characterSection.Height = 60 + characterHint.Height + 8;
            }
            y = characterSection.Bottom + 8;

            if (twoColumns)
            {
                setSection.SetBounds(14, y, columnWidth, 208);
                getSection.SetBounds(14 + columnWidth + 30, y, columnWidth, 208);
                y += 208 + 8;
            }
            else
            {
                setSection.SetBounds(14, y, width, 208);
                getSection.SetBounds(14, y + 216, width, 208);
                y += 216 + 208 + 8;
            }

            int fieldWidth = Math.Max(120, setSection.Width - 78);
            setName.Width = fieldWidth;
            setValue.Width = fieldWidth;
            setFullName.Width = Math.Max(120, setSection.Width - 28);
            setHint.Width = Math.Max(120, setSection.Width - 28);

            int readWidth = Math.Max(120, getSection.Width - 78);
            getName.Width = readWidth;
            getFullName.Width = Math.Max(120, getSection.Width - 28);
            getHint.Width = Math.Max(120, getSection.Width - 28);
            result.Width = Math.Max(120, getSection.Width - 90);

            packedSection.SetBounds(14, y, width, 88);
            packedHint.SetBounds(14, 26, contentWidth, TextHeight(packedHint, contentWidth));
            int packedRow = 26 + packedHint.Height + 8;
            packedIndexLabel.SetBounds(14, packedRow + 6, packedIndexLabel.Width, packedIndexLabel.Height);
            packedIndex.Left = 60;
            packedIndex.Top = packedRow + 2;
            packedValue.Left = 166;
            packedValue.Top = packedRow;
            foreach (TileButton button in packedSection.Controls.OfType<TileButton>())
                button.Top = packedRow + 2;
            packedSection.Height = packedRow + 26 + 8;
            y = packedSection.Bottom + 8;

            int listTop = y;
            int line = 28;
            line = ReflowLine(listIntro, 14, line, contentWidth);
            line = ReflowLine(listFormat, 14, line, contentWidth);
            line = ReflowLine(listNotes, 14, line, contentWidth);

            int buttonWidth = Math.Min(260, Math.Max(160, contentWidth - 210));
            checkListButton.SetBounds(14, line, buttonWidth, 26);
            runListButton.SetBounds(14 + buttonWidth + 10, line, Math.Min(200, contentWidth - buttonWidth - 24), 26);
            line += 34;
            line = ReflowLine(listWarning, 14, line, contentWidth);
            listSection.SetBounds(14, listTop, width, line + 4);

            // Évite une barre horizontale calculée sur un état intermédiaire du Resize.
            PerformLayout();
        }

        private static int TextHeight(Label label, int width)
        {
            return label.GetPreferredSize(new Size(Math.Max(40, width), 0)).Height + 2;
        }

        private static int ReflowLine(Label label, int x, int y, int width)
        {
            int height = TextHeight(label, width);
            label.SetBounds(x, y, width, height);
            return y + height + 6;
        }

        /// <summary>Suit le personnage choisi dans la barre d'outils : l'affiche ici et active les boutons d'écriture/lecture.</summary>
        public void SetCharacter(int slot)
        {
            syncing = true;
            try
            {
                characterBar.SelectedIndex = slot;
            }
            finally
            {
                syncing = false;
            }

            foreach (TileButton button in actionButtons)
                button.Enabled = slot >= 0;

            RefreshFullNames();
        }

        // ---- helpers de layout ----------------------------------------------------------

        private static TextBox TextField(Control parent, string label, int x, int y, int width, string cue)
        {
            parent.Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(x, y + 4) });
            TextBox box = new TextBox { Location = new Point(x + 50, y), Width = width - 50 };
            Theme.Cue(box, cue);
            parent.Controls.Add(box);
            return box;
        }

        private static Label FullNameLabel(Control parent, int x, int y)
        {
            Label label = new Label { Location = new Point(x, y), Size = new Size(440, 18), ForeColor = Theme.Yellow };
            parent.Controls.Add(label);
            return label;
        }

        private void AddButtons(Control parent, int x, int y, params (string Text, Func<Task> Action)[] buttons)
        {
            foreach ((string text, Func<Task> action) in buttons)
            {
                TileButton button = new TileButton { Text = text, Outline = true, Centered = true };
                button.SetBounds(x, y, 112, 26);
                button.Click += async (s, e) => await action();
                actionButtons.Add(button);
                parent.Controls.Add(button);
                x += 120;
            }
        }

        /// <summary>Label multiligne dont la position et la hauteur sont calculées à chaque layout.</summary>
        private static Label NewFlowLabel(string text, Color color, Font font)
        {
            Label label = new Label { Text = text, ForeColor = color, UseMnemonic = false };
            label.Font = font ?? Theme.Body;
            return label;
        }

        // ---- noms -----------------------------------------------------------------------

        private string FullName(ChoiceBar prefix, string typed)
        {
            string name = typed.Trim();
            if (name.Length == 0)
                return string.Empty;

            if (prefix.SelectedIndex == 1)
                return AlreadyPrefixed.IsMatch(name) ? name : "MPPLY_" + name;

            if (prefix.SelectedIndex == 0 && !AlreadyPrefixed.IsMatch(name))
                return host.Slot >= 0 ? $"MP{host.Slot}_{name}" : null;

            return name;
        }

        private void RefreshFullNames()
        {
            UpdateFullName(setFullName, setPrefix, setName);
            UpdateFullName(getFullName, getPrefix, getName);
        }

        private void UpdateFullName(Label label, ChoiceBar prefix, TextBox box)
        {
            string full = FullName(prefix, box.Text);
            label.Text = full == null ? "Choisissez Personnage 1 ou 2 ci-dessus pour voir le nom complet."
                : full.Length == 0 ? string.Empty
                : "Sera utilisé : " + full;
        }

        // ---- stat unique ----------------------------------------------------------------

        private Task SetStat(string type)
        {
            if (setName.Text.Trim().Length == 0)
            {
                MessageBox.Show("Tapez d'abord le nom de la stat, par exemple CHAR_ABILITY_1_UNLCK.", "Écrire une stat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return Task.CompletedTask;
            }

            string name = FullName(setPrefix, setName.Text);
            string raw = setValue.Text.Trim().Replace(',', '.');
            Func<OptionContext, OptionResult> work;
            string required;

            switch (type)
            {
                case "int":
                    if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long intValue) || intValue < int.MinValue || intValue > uint.MaxValue)
                        return Invalid("un nombre entier (32 bits), par exemple -1 ou 100");
                    int value32 = unchecked((int)intValue);
                    required = HandlerKeys.StatSetInt;
                    work = ctx => Single(ctx, r => ctx.WriteInt(r, name, value32));
                    break;
                case "bool":
                    bool flag;
                    if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1") flag = true;
                    else if (raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw == "0") flag = false;
                    else return Invalid("true ou false (ou 1 / 0)");
                    required = HandlerKeys.StatSetBool;
                    work = ctx => Single(ctx, r => ctx.WriteBool(r, name, flag));
                    break;
                default:
                    if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || float.IsNaN(number) || float.IsInfinity(number))
                        return Invalid("un nombre décimal, par exemple 6.66");
                    required = HandlerKeys.StatSetFloat;
                    work = ctx => Single(ctx, r => ctx.WriteFloat(r, name, number));
                    break;
            }

            string details = FlowStats.IsFlow(name) ? "[!] " + FlowStats.Explanation : null;
            return host.RunAsync($"Écrire {type} {name}", new[] { required }, new[] { HandlerKeys.StatGetInt, HandlerKeys.StatSave }, work, details);
        }

        private Task GetStat(string type)
        {
            if (getName.Text.Trim().Length == 0)
            {
                MessageBox.Show("Tapez d'abord le nom de la stat, par exemple CHAR_ABILITY_1_UNLCK.", "Lire une stat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return Task.CompletedTask;
            }

            string name = FullName(getPrefix, getName.Text);
            string handler = type == "int" ? HandlerKeys.StatGetInt : type == "bool" ? HandlerKeys.StatGetBool : HandlerKeys.StatGetFloat;

            return host.RunAsync($"Lire {type} {name}", new[] { handler }, new string[0], ctx =>
            {
                string text;
                bool ok;
                switch (type)
                {
                    case "int":
                        ok = ctx.Engine.TryGetInt(name, out int i);
                        text = ok ? i.ToString(CultureInfo.InvariantCulture) : "illisible";
                        break;
                    case "bool":
                        ok = ctx.Engine.TryGetBool(name, out bool b);
                        text = ok ? b.ToString() : "illisible";
                        break;
                    default:
                        ok = ctx.Engine.TryGetFloat(name, out float f);
                        text = ok ? f.ToString("R", CultureInfo.InvariantCulture) : "illisible";
                        break;
                }

                ctx.Log($"{name} ({type}) = {text}");
                BeginInvoke(new Action(() => result.Text = $"{name} = {text}"));
                return new OptionResult { Written = ok ? 1 : 0 };
            }, null, changesStats: false);
        }

        private Task SetPacked()
        {
            int index = (int)packedIndex.Value;
            bool value = packedValue.SelectedIndex == 0;
            return host.RunAsync($"Écrire packed[{index}] = {value}", new[] { HandlerKeys.PackedSetBool },
                new[] { HandlerKeys.PackedGetBool, HandlerKeys.StatSave },
                ctx => Single(ctx, r => ctx.WritePackedBool(r, index, value)));
        }

        private Task GetPacked()
        {
            int index = (int)packedIndex.Value;
            return host.RunAsync($"Lire packed[{index}]", new[] { HandlerKeys.PackedGetBool }, new string[0], ctx =>
            {
                ctx.Engine.TryGetPackedBool(index, ctx.Slot, out bool value);
                ctx.Log($"packed[{index}] (personnage {ctx.Slot}) = {value}");
                BeginInvoke(new Action(() => result.Text = $"packed[{index}] = {value}"));
                return new OptionResult { Written = 1 };
            }, null, changesStats: false);
        }

        private static OptionResult Single(OptionContext ctx, Action<OptionResult> write)
        {
            OptionResult r = new OptionResult();
            write(r);
            ctx.SaveIfRequested(r);
            return r;
        }

        private static Task Invalid(string expected)
        {
            MessageBox.Show($"La valeur doit être {expected}.", "Écrire une stat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return Task.CompletedTask;
        }

        // ---- listes ---------------------------------------------------------------------

        private bool TryLoadList(string title, out string fileName, out StatListParseResult parsed)
        {
            fileName = null;
            parsed = null;

            string text;
            using (OpenFileDialog dialog = new OpenFileDialog { Title = title, Filter = "Fichier texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*" })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                    return false;

                try
                {
                    FileInfo info = new FileInfo(dialog.FileName);
                    if (info.Length > 4 * 1024 * 1024)
                    {
                        MessageBox.Show("Fichier trop volumineux pour une liste (4 Mo max).", title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }

                    text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
                    fileName = Path.GetFileName(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lecture impossible : " + ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }

            parsed = StatList.Parse(text);
            return true;
        }

        private static string ErrorSummary(StatListParseResult parsed)
        {
            const int shown = 12;
            string text = string.Join("\n", parsed.Errors.Take(shown));
            int more = parsed.Errors.Count - shown;
            return more > 0 ? text + $"\n... et {more:N0} de plus." : text;
        }

        private void CheckList()
        {
            if (!TryLoadList("Vérifier une liste", out string fileName, out StatListParseResult parsed))
                return;

            if (parsed.Errors.Count > 0)
            {
                MessageBox.Show($"{fileName}\n\n{parsed.Errors.Count:N0} problème(s) détecté(s) :\n\n{ErrorSummary(parsed)}\n\nRien n'a été écrit.",
                    "Vérifier une liste", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (parsed.Entries.Count == 0)
            {
                MessageBox.Show($"{fileName}\n\nLe fichier ne contient aucune écriture.", "Vérifier une liste", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show($"{fileName}\n\nListe valide : {StatList.Describe(parsed.Entries)}\n\nRien n'a été écrit.",
                "Vérifier une liste", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task RunList()
        {
            if (!TryLoadList("Exécuter une liste", out string fileName, out StatListParseResult parsed))
                return;

            if (parsed.Errors.Count > 0)
            {
                MessageBox.Show($"{fileName}\n\n{parsed.Errors.Count:N0} problème(s) détecté(s) :\n\n{ErrorSummary(parsed)}\n\nRien n'a été écrit.",
                    "Exécuter une liste", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (parsed.Entries.Count == 0)
            {
                MessageBox.Show($"{fileName}\n\nLe fichier ne contient aucune écriture.", "Exécuter une liste", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<StatEntry> entries = parsed.Entries;
            string details = $"{fileName} : {StatList.Describe(entries)}\n\n{ListWarning}";
            await host.RunAsync($"Liste {fileName} ({entries.Count:N0} écritures)", StatList.RequiredHandlers(entries), StatList.OptionalHandlers,
                ctx => StatList.Run(ctx, entries), details);
        }
    }
}
