using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

public class Program
{
    static void Main(string[] args)
    {
        MainView view = new MainView();
        MainPresenter presenter = PresenterFactory.Create(view);
    }
}

public class Passport
{
    private Passport(string number)
    {
        Number = number;
    }

    public string Number { get; }

    public static Passport Create(string rawInput)
    {
        int quantityDigits = 10;
        string cleaned = rawInput?.Trim().Replace(" ", "") ?? "";
        
        if (string.IsNullOrWhiteSpace(cleaned))
            throw new ArgumentException("Введите серию и номер паспорта");

        if (cleaned.Length != quantityDigits || cleaned.All(char.IsDigit) == false)
            throw new ArgumentException("Неверный формат паспорта");

        return new Passport(cleaned);
    }
}

public record Citizen(bool HasAccess);

public interface IDatabaseContext
{
    DataTable ExecuteQuery(string commandText, params SQLiteParameter[] parameters);
}

public class SQLiteDbContext : IDatabaseContext
{
    private readonly string _connectionString;

    public SQLiteDbContext(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    public DataTable ExecuteQuery(string commandText, params SQLiteParameter[] parameters)
    {
        SQLiteConnection connection = new SQLiteConnection(_connectionString);

        connection.Open();

        SQLiteCommand command = new SQLiteCommand(commandText, connection);

        command.Parameters.AddRange(parameters);

        SQLiteDataAdapter adapter = new SQLiteDataAdapter(command);

        var result = new DataTable();

        adapter.Fill(result);

        return result;
    }
}

public interface IPassportRepository
{
    Citizen FindCitizenByHash(string hash);
}

public interface IPassportService
{
    string ComputeHash(Passport passport);
}

public interface IMainView
{
    void ShowResult(string message);
    void ShowError(string message);
}

public class PassportRepository : IPassportRepository
{
    private readonly IDatabaseContext _context;

    public PassportRepository(IDatabaseContext context)
    {
        _context = context;
    }

    public Citizen FindCitizenByHash(string hash)
    {
        DataTable result = _context.ExecuteQuery("SELECT access_granted FROM passports WHERE num = @hash LIMIT 1",new SQLiteParameter("@hash", hash));

        if (result.Rows == null || result.Rows.Count == 0)
            return null;

        if (result.Rows[0]["access_granted"] is bool access)
            return new Citizen(access);

        return null;
    }
}

public class Sha256PassportService : IPassportService
{
    public string ComputeHash(Passport passport)
    {
        using SHA256 sha = SHA256.Create();

        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(passport.Number));

        return BitConverter.ToString(bytes).Replace("-", "");
    }
}

public class MainPresenter
{
    private readonly IMainView _view;
    private readonly IPassportService _service;
    private readonly IPassportRepository _repository;

    public MainPresenter(IMainView view, IPassportService service, IPassportRepository repository)
    {
        _view = view;
        _service = service;
        _repository = repository;
    }

    public void CheckPassport(string rawPassportInput)
    {
        try
        {
            Passport passport = Passport.Create(rawPassportInput);
            string hash = _service.ComputeHash(passport);
            Citizen citizen = _repository.FindCitizenByHash(hash);

            _view.ShowResult(FormatMessage(passport, citizen));
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

    private string FormatMessage(Passport passport, Citizen citizen) =>
     citizen == null
         ? $"Паспорт {passport.Number} не найден в системе"
         : $"Доступ {(citizen.HasAccess ? "ПРЕДОСТАВЛЕН" : "НЕ ПРЕДОСТАВЛЯЛСЯ")} для паспорта {passport.Number}";
}

public static class PresenterFactory
{
    public static MainPresenter Create(IMainView view)
    {
        string dbPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "","db.sqlite");

        return new MainPresenter(view,new Sha256PassportService(),new PassportRepository(new SQLiteDbContext(dbPath)));
    }
}

public class MainView : IMainView
{
    public MainView()
    {
        var presenter = PresenterFactory.Create(this);
    }

    public void ShowResult(string message)
    {
        MessageBox.Show(message, "Результат", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void ShowError(string message)
    {
        MessageBox.Show(message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
