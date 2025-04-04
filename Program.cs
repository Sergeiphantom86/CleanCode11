public class Passport
{
    public string Number { get; }

    public Passport(string rawData)
    {
        var cleaned = rawData?.Trim().Replace(" ", "") ?? "";

        if (cleaned.Length < 10)
            throw new ArgumentException("Неверный формат паспорта");

        Number = cleaned;
    }
}

public interface IPassportValidator
{
    bool Validate(string input);
}

public class PassportValidator : IPassportValidator
{
    public bool Validate(string input) => 
        string.IsNullOrWhiteSpace(input) == false;
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

    public bool CheckAccess(string hash)
    {
        SQLiteConnection connection = new SQLiteConnection(GetConnectionString());

        connection.Open();

        SQLiteCommand command = new SQLiteCommand($"SELECT access_granted FROM passports WHERE hash = {Hash} LIMIT 1",connection);

        command.Parameters.AddWithValue(Hash, hash);
        return Convert.ToBoolean(command.ExecuteScalar());
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
    private readonly IPassportValidator _validator;
    private readonly PassportHashService _hashService;
    private readonly PassportRepository _repository;

    public PassportPresenter(IPassportView view, IPassportValidator validator, PassportHashService hashService, PassportRepository repository)
    {
        _view = view;
        _validator = validator;
        _hashService = hashService;
        _repository = repository;
    }

    public void CheckPassport()
    {
        try
        {
            if (_validator.Validate(_view.PassportInput) == false)
            {
                _view.ShowError("Введите серию и номер паспорта");
                return;
            }

            var passport = new Passport(_view.PassportInput);
            var hash = _hashService.ComputeHash(passport);
            var accessGranted = _repository.CheckAccess(hash);

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
        var status = accessGranted ? "ПРЕДОСТАВЛЕН" : "НЕ ПРЕДОСТАВЛЯЛСЯ";
        return $"По паспорту «{passport.Number}» доступ к бюллетеню {status}";
    }
}

public partial class MainForm : IPassportView
{
    private PassportPresenter _presenter;

    public MainForm()
    {
        _presenter = new PassportPresenter(this, new PassportValidator(), new PassportHashService(), new PassportRepository());
    }

    public string PassportInput => 
        _txtPassport.Text;

    public void ShowResult(string message) =>
        message;

    public void ShowError(string message) =>
        MessageBox.Show(message);

    private void CheckPassportHandler(object sender) => 
        _presenter.CheckPassport();
}
