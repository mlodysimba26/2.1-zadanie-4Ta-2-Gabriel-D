using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PasswordGenerator;

/// <summary>
/// Logika interakcji dla MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
    private const string NumberChars = "0123456789";
    private const string SpecialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";
    private const string AmbiguousChars = "1lI|0O8B";

    private readonly DispatcherTimer _copyToastTimer;
    private bool _isInitialized = false;

    public MainWindow()
    {
        InitializeComponent();

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

    private void OptionChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        GeneratePassword();
    }

    private void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        GeneratePassword();
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtPassword.Text) || lblWarning.Visibility == Visibility.Visible)
        {
            return;
        }

        try
        {
            Clipboard.SetText(txtPassword.Text);
            lblCopyStatus.Text = "✓ Skopiowano do schowka!";
            lblCopyStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            lblCopyStatus.Visibility = Visibility.Visible;
            _copyToastTimer.Stop();
            _copyToastTimer.Start();
        }
        catch (Exception)
        {
            lblCopyStatus.Text = "⚠️ Błąd kopiowania do schowka.";
            lblCopyStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            lblCopyStatus.Visibility = Visibility.Visible;
            _copyToastTimer.Stop();
            _copyToastTimer.Start();
        }
    }

    private void GeneratePassword()
    {
        if (!_isInitialized) return;

        bool useUpper = chkUppercase.IsChecked == true;
        bool useLower = chkLowercase.IsChecked == true;
        bool useNumbers = chkNumbers.IsChecked == true;
        bool useSpecial = chkSpecial.IsChecked == true;
        bool excludeAmbiguous = chkExcludeAmbiguous.IsChecked == true;

        if (!useUpper && !useLower && !useNumbers && !useSpecial)
        {
            lblWarning.Visibility = Visibility.Visible;
            txtPassword.Text = "Wybierz opcje!";
            pbStrength.Value = 0;
            lblStrength.Text = "Brak opcji";
            lblStrength.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            return;
        }

        lblWarning.Visibility = Visibility.Collapsed;

        int length = (int)sliderLength.Value;

        // Przygotuj pule znaków
        List<string> activeCategories = new List<string>();

        if (useUpper) activeCategories.Add(FilterChars(UppercaseChars, excludeAmbiguous));
        if (useLower) activeCategories.Add(FilterChars(LowercaseChars, excludeAmbiguous));
        if (useNumbers) activeCategories.Add(FilterChars(NumberChars, excludeAmbiguous));
        if (useSpecial) activeCategories.Add(FilterChars(SpecialChars, excludeAmbiguous));

        // Połączona pula
        StringBuilder fullPoolBuilder = new StringBuilder();
        foreach (var cat in activeCategories)
        {
            fullPoolBuilder.Append(cat);
        }
        string fullPool = fullPoolBuilder.ToString();

        if (string.IsNullOrEmpty(fullPool))
        {
            lblWarning.Visibility = Visibility.Visible;
            txtPassword.Text = "Pusta pula znaków!";
            return;
        }

        List<char> passwordChars = new List<char>(length);

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

        string generatedPassword = new string(passwordChars.ToArray());
        txtPassword.Text = generatedPassword;

        // Ocenienie siły hasła
        UpdatePasswordStrength(generatedPassword, fullPool.Length);
    }

    private static string FilterChars(string source, bool excludeAmbiguous)
    {
        if (!excludeAmbiguous) return source;
        return new string(source.Where(c => !AmbiguousChars.Contains(c)).ToArray());
    }

    private void UpdatePasswordStrength(string password, int poolSize)
    {
        if (string.IsNullOrEmpty(password) || poolSize <= 0)
        {
            pbStrength.Value = 0;
            lblStrength.Text = "—";
            return;
        }

        // Obliczenie entropii w bitach: L * log2(R)
        double entropy = password.Length * Math.Log2(poolSize);

        // Mapowanie siły hasła
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
            colorHex = "#F59E0B"; // Żółty/Bursztynowy
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
            colorHex = "#06B6D4"; // Błękit/Cyan
            progressVal = 100;
        }

        pbStrength.Value = progressVal;
        SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        pbStrength.Foreground = brush;
        lblStrength.Text = $"{strengthText} (~{(int)entropy} bitów)";
        lblStrength.Foreground = brush;
    }
}