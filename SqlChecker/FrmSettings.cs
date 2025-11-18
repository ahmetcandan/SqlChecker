using MsSqlAnalyze;

namespace SqlChecker;

public partial class FrmSettings : Form
{
    public Settings Settings { get; }

    public FrmSettings(Settings? settings = null)
    {
        InitializeComponent();
        Settings = settings is not null
            ? new()
            {
                ConnectionString = settings.ConnectionString
            }
            : new();
        DialogResult = DialogResult.Cancel;
    }

    private async void BtnSave_Click(object sender, EventArgs e)
    {
        Settings.ConnectionString = txtConnectionString.Text;
        await File.WriteAllTextAsync("settings.json", System.Text.Json.JsonSerializer.Serialize(Settings));
        DialogResult = DialogResult.OK;
        Close();
    }

    private void FrmSettings_Load(object sender, EventArgs e) => txtConnectionString.Text = Settings?.ConnectionString;
}
