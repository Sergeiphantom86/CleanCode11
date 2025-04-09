public class Passport
{
    public Passport(string rawData)
    {
        string cleaned = rawData?.Trim().Replace(" ", "") ?? "";

        if (string.IsNullOrWhiteSpace(cleaned))
            throw new ArgumentException("Введите серию и номер паспорта");

        if (cleaned.Length != 10 || cleaned.All(char.IsDigit) == false)
            throw new ArgumentException("Неверный формат паспорта. Требуется 10 цифр");

        Number = cleaned;
    }

    public string Number { get; }
}

public class PassportHashService
{
    public string ComputeHash(Passport passport)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(passport.Number));
        return BitConverter.ToString(bytes).Replace("-", "");
    }
}

public class PassportRepository
{
    private const string Hash = "@hash";

    public bool? CheckAccess(string hash)
    {
        using var connection = new SQLiteConnection(GetConnectionString());

        connection.Open();

        using var command = new SQLiteCommand(
            $"SELECT access_granted FROM passports WHERE hash = {Hash} LIMIT 1",
            connection);

        command.Parameters.AddWithValue(Hash, hash);

        var result = command.ExecuteScalar();

        return result != null ? (bool?)Convert.ToBoolean(result) : null;
    }

    private string GetConnectionString() =>
        $"Data Source={Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db.sqlite")}";
}

public interface IPassportView
{
    string PassportInput { get; }
    void ShowResult(string message);
    void ShowError(string message);
}

public class PassportPresenter
{
    private readonly IPassportView _view;
    private readonly PassportHashService _hashService;
    private readonly PassportRepository _repository;

    public PassportPresenter(IPassportView view, PassportHashService hashService, PassportRepository repository)
    {
        _view = view;
        _hashService = hashService;
        _repository = repository;
    }

    public void CheckPassport()
    {
        try
        {
            Passport passport = new Passport(_view.PassportInput);
            string hash = _hashService.ComputeHash(passport);
            bool accessGranted = _repository.CheckAccess(hash);

            _view.ShowResult(FormatResult(passport, accessGranted));
        }
        catch (ArgumentException exception)
        {
            _view.ShowError(exception.Message);
        }
        catch (SQLiteException exception) when (exception.ErrorCode == 1)
        {
            _view.ShowError("Файл базы данных не найден");
        }
        catch (Exception exception)
        {
            _view.ShowError($"Ошибка: {exception.Message}");
        }
    }

    private string FormatResult(Passport passport, bool accessGranted)
    {
        string status = accessGranted ? "ПРЕДОСТАВЛЕН" : "НЕ ПРЕДОСТАВЛЯЛСЯ";
        return $"По паспорту «{passport.Number}» доступ к бюллетеню {status}";
    }
}

public partial class MainForm : IPassportView
{
    private PassportPresenter _presenter;

    public MainForm()
    {
        _presenter = new PassportPresenter(this,new PassportHashService(),new PassportRepository());
    }

    public string PassportInput => passportTextBox.Text;

    public void ShowResult(string message)
    {
        resultLabel.Text = message;
        resultLabel.Visible = true;
    }

    public void ShowError(string message)
    {
        MessageBox.Show(message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void CheckButtonClick()
    {
        _presenter.CheckPassport();
    }
}
