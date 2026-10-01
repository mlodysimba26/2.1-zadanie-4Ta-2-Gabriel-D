using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PasswordGenerator;

public class PasswordHistoryItem
{
    public string Password { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public string ModeTag { get; set; } = "";
    public string StrengthTag { get; set; } = "";
    public Brush StrengthBrush { get; set; } = Brushes.LightGreen;
}

public partial class MainWindow : Window
{
    private const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
    private const string NumberChars = "0123456789";
    private const string SpecialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";
    private const string AmbiguousChars = "1lI|0O8B";

    private static readonly string[] WordList = new[]
    {
        "akacja", "alarm", "albatros", "aleja", "alpaka", "ametyst", "antena", "aparat", "arbuz",
        "atlas", "aurora", "balkon", "bambus", "banan", "barka", "baryton", "bateria", "baobab", "bizon",
        "blask", "bluszcz", "blyskawica", "bocian", "brama", "bursztyn", "burza", "chmura", "chmiel", "cisza",
        "cytryna", "czapla", "delfin", "diament", "dolina", "doniczka", "drzewo", "dukat", "dywan", "dzwonek",
        "echo", "ekran", "emalia", "eskapada", "fala", "faraon", "fartuch", "fiolek", "flaming", "flota",
        "fontanna", "forteca", "fregata", "galaktyka", "ganek", "gepard", "giewont", "gitar", "glaz", "goryl",
        "granat", "grot", "gryf", "grzmot", "gwiazda", "harfa", "heliotrop", "herbata", "horyzont", "hotel",
        "huragan", "ibisz", "iglica", "igloo", "iluzja", "impuls", "indyk", "irys", "iskra",
        "jagoda", "jantar", "jaskinia", "jastrzab", "jezioro", "jodla", "kajak", "kakao", "kamien", "kamyk",
        "kanion", "kapitan", "kapsula", "karawana", "karmel", "kaskada", "kasztan", "katedra", "kawa", "klawisz",
        "klejnot", "klon", "kobalt", "koliber", "kometa", "kompas", "konwalia", "koral", "korona", "kosmos",
        "kotwica", "kratka", "krysztal", "ksiezyc", "kwadrat", "kwarc", "kwiat", "labirynt", "lampart", "latarnia",
        "lawenda", "lazurowy", "legenda", "lemur", "leopard", "limonka", "listek", "lodowiec", "lotnisko",
        "magnat", "magnolia", "majatek", "makrela", "malina", "mamut", "mandarynka", "marmur", "maska", "maszt",
        "melodia", "meteor", "miecz", "migdal", "mineral", "molo", "moneta", "morze", "most", "motyl",
        "muszla", "muzyka", "nakretka", "namiot", "narcyz", "nawias", "neon", "neptun", "niebo", "niedzwiedz",
        "nutka", "oaza", "obelisk", "ocean", "odkrycie", "ogien", "ogrod", "okret", "oliwka", "opal",
        "orfeusz", "orzech", "orzel", "osada", "palma", "pantera", "papirus", "papuga", "parasol", "park",
        "pelikan", "perla", "piasek", "pierscien", "piorun", "piramida", "planeta", "platyn", "plaza", "plecak",
        "plomyk", "podroz", "polana", "potok", "promien", "przystan", "puchacz", "pustynia", "radar", "rafa",
        "rakieta", "renifer", "rubin", "rys", "rzeka", "safari", "szafir", "sanie", "sekret", "serce",
        "smok", "snieg", "sokol", "sonar", "sosna", "sowa", "spacer", "srebro", "statek",
        "step", "stokrotka", "strumyk", "sygnal", "szalotka", "szczyt", "szmaragd", "szron", "talerz",
        "tarcza", "teatr", "teleskop", "temat", "tecza", "topaz", "tornado", "traper", "trofeum", "truskawka",
        "tulipan", "turniej", "twierdza", "tygrys", "wagon", "watra", "wicher", "widok", "wieza",
        "wilk", "wioska", "wodospad", "wojownik", "wulkan", "wyprawa", "wyspa", "wzgorze", "zagiel", "zamek",
        "zegar", "zegluga", "ziemia", "zloto", "zmija", "zorza", "zrodlo", "zubr", "zwiadowca", "zyrafa"
    };

    private static readonly string[] CommonBadPasswords = new[]
    {
        "123456", "12345678", "password", "haslo", "qwerty", "admin", "123456789", "letmein", "polska",
        "iloveyou", "zaq1@wsx", "qwertyuiop", "111111", "123123", "master", "dragon", "football", "shadow"
    };

    private readonly ObservableCollection<PasswordHistoryItem> _history = new();
    private readonly DispatcherTimer _copyToastTimer;
    private bool _isInitialized = false;

    private string _currentRawPassword = "";
    private bool _isPasswordMasked = false;
    private string _currentGeneratorMode = "Chars"; // "Chars", "Words", "Pin"

    public MainWindow()
    {
        InitializeComponent();

        icHistory.ItemsSource = _history;

        _copyToastTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _copyToastTimer.Tick += (s, e) =>
        {
            lblCopyStatus.Visibility = Visibility.Collapsed;
            _copyToastTimer.Stop();
        };

        _isInitialized = true;
        GeneratePassword();
    }

    // ==========================================
    // NAWIGACJA ZAKŁADEK (Generator / Tester / Historia)
    // ==========================================
    private void TabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tabTag) return;

        // Reset styli zakładek
        Brush inactiveBg = Brushes.Transparent;
        Brush inactiveFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
        Brush activeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));
        Brush activeFg = Brushes.White;

        btnTabGenerator.Background = inactiveBg;
        btnTabGenerator.Foreground = inactiveFg;
        btnTabTester.Background = inactiveBg;
        btnTabTester.Foreground = inactiveFg;
        btnTabHistory.Background = inactiveBg;
        btnTabHistory.Foreground = inactiveFg;

        panelGenerator.Visibility = Visibility.Collapsed;
        panelTester.Visibility = Visibility.Collapsed;
        panelHistory.Visibility = Visibility.Collapsed;

        btn.Background = activeBg;
        btn.Foreground = activeFg;

        switch (tabTag)
        {
            case "Generator":
                panelGenerator.Visibility = Visibility.Visible;
                break;
            case "Tester":
                panelTester.Visibility = Visibility.Visible;
                if (string.IsNullOrEmpty(txtTestPassword.Text) && !string.IsNullOrEmpty(_currentRawPassword))
                {
                    txtTestPassword.Text = _currentRawPassword;
                }
                EvaluateTestedPassword(txtTestPassword.Text);
                break;
            case "History":
                panelHistory.Visibility = Visibility.Visible;
                UpdateHistoryEmptyState();
                break;
        }
    }

    // ==========================================
    // PRZEŁĄCZANIE TRYBÓW GENERATORA
    // ==========================================
    private void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string mode) return;

        _currentGeneratorMode = mode;

        Brush inactiveBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
        Brush inactiveFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
        Brush inactiveBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));

        Brush activeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));
        Brush activeFg = Brushes.White;
        Brush activeBorder = activeBg;

        btnModeChars.Background = inactiveBg;
        btnModeChars.Foreground = inactiveFg;
        btnModeChars.BorderBrush = inactiveBorder;

        btnModeWords.Background = inactiveBg;
        btnModeWords.Foreground = inactiveFg;
        btnModeWords.BorderBrush = inactiveBorder;

        btnModePin.Background = inactiveBg;
        btnModePin.Foreground = inactiveFg;
        btnModePin.BorderBrush = inactiveBorder;

        btn.Background = activeBg;
        btn.Foreground = activeFg;
        btn.BorderBrush = activeBorder;

        panelCharsOptions.Visibility = Visibility.Collapsed;
        panelWordsOptions.Visibility = Visibility.Collapsed;
        panelPinOptions.Visibility = Visibility.Collapsed;

        switch (mode)
        {
            case "Chars":
                panelCharsOptions.Visibility = Visibility.Visible;
                break;
            case "Words":
                panelWordsOptions.Visibility = Visibility.Visible;
                break;
            case "Pin":
                panelPinOptions.Visibility = Visibility.Visible;
                break;
        }

        GeneratePassword();
    }

    private void OptionChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        GeneratePassword();
    }

    private void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        GeneratePassword();
    }

    // ==========================================
    // GENEROWANIE HASŁA
    // ==========================================
    private void GeneratePassword()
    {
        if (!_isInitialized) return;

        string result = "";
        double entropy = 0;
        string modeBadge = "";

        switch (_currentGeneratorMode)
        {
            case "Chars":
                result = GenerateCharactersPassword(out entropy, out modeBadge);
                break;
            case "Words":
                result = GeneratePassphrase(out entropy, out modeBadge);
                break;
            case "Pin":
                result = GeneratePin(out entropy, out modeBadge);
                break;
        }

        if (string.IsNullOrEmpty(result))
        {
            return;
        }

        _currentRawPassword = result;
        UpdatePasswordDisplay();

        // Siła hasła i czas na złamanie
        UpdatePasswordStrength(entropy, result);

        // Dodaj do historii sesji
        AddHistoryItem(result, modeBadge, lblStrength.Text, pbStrength.Foreground);
    }

    private string GenerateCharactersPassword(out double entropy, out string modeBadge)
    {
        entropy = 0;
        modeBadge = "Znaki";

        bool useUpper = chkUppercase.IsChecked == true;
        bool useLower = chkLowercase.IsChecked == true;
        bool useNumbers = chkNumbers.IsChecked == true;
        bool useSpecial = chkSpecial.IsChecked == true;
        bool excludeAmbiguous = chkExcludeAmbiguous.IsChecked == true;

        if (!useUpper && !useLower && !useNumbers && !useSpecial)
        {
            lblWarning.Visibility = Visibility.Visible;
            _currentRawPassword = "Wybierz opcje!";
            UpdatePasswordDisplay();
            pbStrength.Value = 0;
            lblStrength.Text = "Brak opcji";
            lblCrackTime.Text = "—";
            lblStrength.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            return "";
        }

        lblWarning.Visibility = Visibility.Collapsed;
        int length = (int)sliderLength.Value;
        modeBadge = $"Znaki [{length}]";

        List<string> activeCategories = new();
        if (useUpper) activeCategories.Add(FilterChars(UppercaseChars, excludeAmbiguous));
        if (useLower) activeCategories.Add(FilterChars(LowercaseChars, excludeAmbiguous));
        if (useNumbers) activeCategories.Add(FilterChars(NumberChars, excludeAmbiguous));
        if (useSpecial) activeCategories.Add(FilterChars(SpecialChars, excludeAmbiguous));

        StringBuilder fullPoolBuilder = new();
        foreach (var cat in activeCategories)
        {
            fullPoolBuilder.Append(cat);
        }
        string fullPool = fullPoolBuilder.ToString();

        if (string.IsNullOrEmpty(fullPool))
        {
            lblWarning.Visibility = Visibility.Visible;
            _currentRawPassword = "Pusta pula znaków!";
            UpdatePasswordDisplay();
            return "";
        }

        List<char> passwordChars = new(length);

        // Gwarancja: co najmniej jeden znak z każdej wybranej kategorii
        foreach (var cat in activeCategories)
        {
            if (cat.Length > 0 && passwordChars.Count < length)
            {
                int randomIndex = RandomNumberGenerator.GetInt32(cat.Length);
                passwordChars.Add(cat[randomIndex]);
            }
        }

        // Dopełnij resztę znaków losowo z pełnej puli
        while (passwordChars.Count < length)
        {
            int randomIndex = RandomNumberGenerator.GetInt32(fullPool.Length);
            passwordChars.Add(fullPool[randomIndex]);
        }

        // Przetasuj tablicę (Fisher-Yates) za pomocą kryptograficznego RNG
        for (int i = passwordChars.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (passwordChars[i], passwordChars[j]) = (passwordChars[j], passwordChars[i]);
        }

        entropy = length * Math.Log2(fullPool.Length);
        return new string(passwordChars.ToArray());
    }

    private string GeneratePassphrase(out double entropy, out string modeBadge)
    {
        lblWarning.Visibility = Visibility.Collapsed;
        int wordCount = (int)sliderWordCount.Value;
        modeBadge = $"Passphrase [{wordCount}]";

        string separator = "-";
        if (cmbSeparator.SelectedItem is ComboBoxItem item && item.Tag != null)
        {
            separator = item.Tag.ToString() ?? "-";
        }

        bool capitalize = chkCapitalizeWords.IsChecked == true;
        bool addNumber = chkAddNumberToPassphrase.IsChecked == true;

        List<string> selectedWords = new(wordCount);
        for (int i = 0; i < wordCount; i++)
        {
            int idx = RandomNumberGenerator.GetInt32(WordList.Length);
            string word = WordList[idx];
            if (capitalize && word.Length > 0)
            {
                word = char.ToUpperInvariant(word[0]) + word.Substring(1);
            }
            selectedWords.Add(word);
        }

        string result = string.Join(separator, selectedWords);

        if (addNumber)
        {
            int randomNum = RandomNumberGenerator.GetInt32(10, 100);
            result = string.IsNullOrEmpty(separator) ? $"{result}{randomNum}" : $"{result}{separator}{randomNum}";
        }

        // Entropia diceware: K * log2(liczba_slow) + ewentualnie log2(90) dla liczby
        entropy = wordCount * Math.Log2(WordList.Length) + (addNumber ? Math.Log2(90) : 0);
        return result;
    }

    private string GeneratePin(out double entropy, out string modeBadge)
    {
        lblWarning.Visibility = Visibility.Collapsed;
        int length = (int)sliderPinLength.Value;
        modeBadge = $"PIN [{length}]";

        bool avoidRepeats = chkAvoidRepeatsPin.IsChecked == true;
        char[] pin = new char[length];
        char lastChar = '\0';

        for (int i = 0; i < length; i++)
        {
            char nextChar;
            do
            {
                int digit = RandomNumberGenerator.GetInt32(10);
                nextChar = (char)('0' + digit);
            } while (avoidRepeats && i > 0 && nextChar == lastChar);

            pin[i] = nextChar;
            lastChar = nextChar;
        }

        entropy = length * Math.Log2(avoidRepeats ? 9 : 10);
        return new string(pin);
    }

    private static string FilterChars(string source, bool excludeAmbiguous)
    {
        if (!excludeAmbiguous) return source;
        return new string(source.Where(c => !AmbiguousChars.Contains(c)).ToArray());
    }

    // ==========================================
    // WIDOCZNOŚĆ I KOPIOWANIE
    // ==========================================
    private void UpdatePasswordDisplay()
    {
        if (_isPasswordMasked)
        {
            txtPassword.Text = new string('•', _currentRawPassword.Length);
            btnToggleMask.Content = "🙈";
        }
        else
        {
            txtPassword.Text = _currentRawPassword;
            btnToggleMask.Content = "👁️";
        }
    }

    private void BtnToggleMask_Click(object sender, RoutedEventArgs e)
    {
        _isPasswordMasked = !_isPasswordMasked;
        UpdatePasswordDisplay();
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentRawPassword) || lblWarning.Visibility == Visibility.Visible)
        {
            return;
        }

        CopyToClipboard(_currentRawPassword);
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
            lblCopyStatus.Text = "✓ Skopiowano do schowka!";
            lblCopyStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            lblCopyStatus.Visibility = Visibility.Visible;
            _copyToastTimer.Stop();
            _copyToastTimer.Start();
        }
        catch
        {
            lblCopyStatus.Text = "⚠️ Błąd schowka Windows.";
            lblCopyStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            lblCopyStatus.Visibility = Visibility.Visible;
            _copyToastTimer.Stop();
            _copyToastTimer.Start();
        }
    }

    // ==========================================
    // OCENA SIŁY I CZASU ZŁAMANIA
    // ==========================================
    private void UpdatePasswordStrength(double entropy, string password)
    {
        if (string.IsNullOrEmpty(password) || entropy <= 0)
        {
            pbStrength.Value = 0;
            lblStrength.Text = "—";
            lblCrackTime.Text = "—";
            return;
        }

        string strengthText;
        string colorHex;
        double progressVal;

        if (entropy < 35)
        {
            strengthText = "Bardzo słabe";
            colorHex = "#EF4444"; // Czerwony
            progressVal = 20;
        }
        else if (entropy < 55)
        {
            strengthText = "Słabe";
            colorHex = "#F97316"; // Pomarańczowy
            progressVal = 40;
        }
        else if (entropy < 75)
        {
            strengthText = "Średnie";
            colorHex = "#F59E0B"; // Żółty
            progressVal = 65;
        }
        else if (entropy < 100)
        {
            strengthText = "Silne";
            colorHex = "#10B981"; // Zielony
            progressVal = 85;
        }
        else
        {
            strengthText = "Bardzo silne";
            colorHex = "#06B6D4"; // Cyan
            progressVal = 100;
        }

        pbStrength.Value = progressVal;
        SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        pbStrength.Foreground = brush;
        lblStrength.Text = $"{strengthText} (~{(int)entropy} bitów)";
        lblStrength.Foreground = brush;

        // Szacowanie czasu złamania: 100 mld (10^11) prób/s
        double combinations = Math.Pow(2, entropy);
        double seconds = (combinations / 2.0) / 1e11;
        lblCrackTime.Text = FormatCrackTime(seconds);
    }

    public static string FormatCrackTime(double seconds)
    {
        if (double.IsInfinity(seconds) || seconds > 1e25)
            return "Więcej niż wiek wszechświata";
        if (seconds < 0.001)
            return "Natychmiast (< 1 ms)";
        if (seconds < 1)
            return "Ułamek sekundy";
        if (seconds < 60)
            return $"ok. {Math.Max(1, (int)seconds)} sek.";

        double minutes = seconds / 60;
        if (minutes < 60)
            return $"ok. {(int)minutes} min.";

        double hours = minutes / 60;
        if (hours < 24)
            return $"ok. {(int)hours} godz.";

        double days = hours / 24;
        if (days < 365)
            return $"ok. {(int)days} dni";

        double years = days / 365.25;
        if (years < 1000)
            return $"ok. {(int)years} lat";
        if (years < 1_000_000)
            return $"ok. {years / 1000:0.#} tys. lat";
        if (years < 1_000_000_000)
            return $"ok. {years / 1_000_000:0.#} mln lat";
        if (years < 1_000_000_000_000)
            return $"ok. {years / 1_000_000_000:0.#} mld lat";

        return "Biliony lat";
    }

    // ==========================================
    // HISTORIA SESJI
    // ==========================================
    private void AddHistoryItem(string password, string modeTag, string strengthTag, Brush strengthBrush)
    {
        // Unikaj dublowania identycznego hasła pod rząd
        if (_history.Count > 0 && _history[0].Password == password)
        {
            return;
        }

        var item = new PasswordHistoryItem
        {
            Password = password,
            ModeTag = modeTag,
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            StrengthTag = strengthTag.Split('(')[0].Trim(),
            StrengthBrush = strengthBrush
        };

        _history.Insert(0, item);

        // Ogranicz historię do ostatnich 25 haseł
        while (_history.Count > 25)
        {
            _history.RemoveAt(_history.Count - 1);
        }

        btnTabHistory.Content = $"📜 Historia ({_history.Count})";
        UpdateHistoryEmptyState();
    }

    private void UpdateHistoryEmptyState()
    {
        lblEmptyHistory.Visibility = _history.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
    {
        _history.Clear();
        btnTabHistory.Content = "📜 Historia (0)";
        UpdateHistoryEmptyState();
    }

    private void BtnCopyHistoryItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string pwd)
        {
            CopyToClipboard(pwd);
        }
    }

    // ==========================================
    // MODUŁ TESTERA / AUDYTORA WŁASNYCH HASEŁ
    // ==========================================
    private void TxtTestPassword_TextChanged(object sender, TextChangedEventArgs e)
    {
        EvaluateTestedPassword(txtTestPassword.Text);
    }

    private void BtnPasteToTester_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                txtTestPassword.Text = Clipboard.GetText().Trim();
            }
        }
        catch
        {
            // Ignoruj błędy schowka
        }
    }

    private void EvaluateTestedPassword(string pwd)
    {
        if (string.IsNullOrEmpty(pwd))
        {
            pbTestStrength.Value = 0;
            lblTestStrength.Text = "Wpisz hasło";
            lblTestStrength.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
            lblTestCrackTime.Text = "Brak danych";
            ResetTestChecklist();
            return;
        }

        bool hasUpper = pwd.Any(char.IsUpper);
        bool hasLower = pwd.Any(char.IsLower);
        bool hasDigit = pwd.Any(char.IsDigit);
        bool hasSpecial = pwd.Any(c => !char.IsLetterOrDigit(c));
        bool isLongEnough = pwd.Length >= 12;

        // Wykrywanie wzorców
        string lowerPwd = pwd.ToLowerInvariant();
        bool hasCommonWord = CommonBadPasswords.Any(bad => lowerPwd.Contains(bad));
        bool hasRepetitions = HasRepetitiveChars(pwd);
        bool patternsPass = !hasCommonWord && !hasRepetitions;

        // Aktualizacja checklisty
        UpdateCheckItem(lblCheckLength, isLongEnough, $"Długość min. 12 znaków (obecnie {pwd.Length})");
        UpdateCheckItem(lblCheckUpper, hasUpper, "Zawiera wielkie litery (A-Z)");
        UpdateCheckItem(lblCheckLower, hasLower, "Zawiera małe litery (a-z)");
        UpdateCheckItem(lblCheckDigits, hasDigit, "Zawiera cyfry (0-9)");
        UpdateCheckItem(lblCheckSpecial, hasSpecial, "Zawiera znaki specjalne (!@#$...)");

        if (hasCommonWord)
        {
            UpdateCheckItem(lblCheckPatterns, false, "Wykryto popularne słowo lub sekwencję (np. 1234, admin, haslo)!");
        }
        else if (hasRepetitions)
        {
            UpdateCheckItem(lblCheckPatterns, false, "Zawiera powtarzające się ciągi tych samych znaków!");
        }
        else
        {
            UpdateCheckItem(lblCheckPatterns, true, "Brak popularnych słów i powtórzeń");
        }

        // Pula znaków i entropia
        int poolSize = 0;
        if (hasLower) poolSize += 26;
        if (hasUpper) poolSize += 26;
        if (hasDigit) poolSize += 10;
        if (hasSpecial) poolSize += 33;
        if (poolSize == 0) poolSize = 10;

        double entropy = pwd.Length * Math.Log2(poolSize);

        // Kary za słabe wzorce
        if (hasCommonWord) entropy = Math.Min(entropy, 20);
        if (hasRepetitions) entropy = Math.Max(10, entropy - 25);

        // Formatowanie oceny
        string statusText;
        string colorHex;
        double progress;

        if (entropy < 35 || pwd.Length < 6)
        {
            statusText = "Krytycznie słabe!";
            colorHex = "#EF4444";
            progress = 15;
            lblTestTip.Text = "To hasło można odgadnąć niemal natychmiast. Zwiększ długość do min. 14 znaków i dodaj cyfry oraz symbole.";
        }
        else if (entropy < 55)
        {
            statusText = "Słabe";
            colorHex = "#F97316";
            progress = 35;
            lblTestTip.Text = "Hasło jest za krótkie lub ma zbyt prosty zestaw znaków. Dodaj znaki specjalne lub połącz kilka losowych słów.";
        }
        else if (entropy < 75)
        {
            statusText = "Umiarkowane / Dobre";
            colorHex = "#F59E0B";
            progress = 65;
            lblTestTip.Text = "Dobra ochrona przed typowymi atakami. Aby uzyskać status bardzo silnego, wydłuż je do 16 znaków.";
        }
        else if (entropy < 95)
        {
            statusText = "Silne!";
            colorHex = "#10B981";
            progress = 85;
            lblTestTip.Text = "Świetne hasło! Spełnia wysokie normy bezpieczeństwa większości serwisów internetowych.";
        }
        else
        {
            statusText = "Niezwykle silne!";
            colorHex = "#06B6D4";
            progress = 100;
            lblTestTip.Text = "Doskonałe zabezpieczenie. Odporne na zaawansowane ataki ze stacjami GPU o wysokiej mocy obliczeniowej.";
        }

        pbTestStrength.Value = progress;
        SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        pbTestStrength.Foreground = brush;
        lblTestStrength.Text = $"{statusText} (~{(int)entropy} bitów)";
        lblTestStrength.Foreground = brush;

        double combinations = Math.Pow(2, entropy);
        double seconds = (combinations / 2.0) / 1e11;
        lblTestCrackTime.Text = FormatCrackTime(seconds);
    }

    private static bool HasRepetitiveChars(string s)
    {
        if (s.Length < 3) return false;
        int repeatCount = 1;
        for (int i = 1; i < s.Length; i++)
        {
            if (s[i] == s[i - 1])
            {
                repeatCount++;
                if (repeatCount >= 3) return true;
            }
            else
            {
                repeatCount = 1;
            }
        }
        return false;
    }

    private static void UpdateCheckItem(TextBlock tb, bool ok, string text)
    {
        tb.Text = ok ? $"✔️ {text}" : $"❌ {text}";
        tb.Foreground = ok 
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")) 
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
    }

    private void ResetTestChecklist()
    {
        lblCheckLength.Text = "⚪ Co najmniej 12 znaków długości";
        lblCheckUpper.Text = "⚪ Zawiera wielkie litery (A-Z)";
        lblCheckLower.Text = "⚪ Zawiera małe litery (a-z)";
        lblCheckDigits.Text = "⚪ Zawiera cyfry (0-9)";
        lblCheckSpecial.Text = "⚪ Zawiera znaki specjalne (!@#$...)";
        lblCheckPatterns.Text = "⚪ Brak popularnych słów i powtarzających się schematów";

        Brush neutral = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
        lblCheckLength.Foreground = neutral;
        lblCheckUpper.Foreground = neutral;
        lblCheckLower.Foreground = neutral;
        lblCheckDigits.Foreground = neutral;
        lblCheckSpecial.Foreground = neutral;
        lblCheckPatterns.Foreground = neutral;

        lblTestTip.Text = "Wpisz lub wklej hasło powyżej, aby przeanalizować jego odporność.";
    }
}