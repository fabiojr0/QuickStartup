using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickStartup.Models;

public class Profile : INotifyPropertyChanged
{
    private string _name = "Novo Perfil";
    private bool _isDefault;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public ObservableCollection<AppItem> Apps { get; set; } = new();

    public bool IsDefault
    {
        get => _isDefault;
        set { _isDefault = value; OnPropertyChanged(); }
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Profile Clone()
    {
        var clone = new Profile
        {
            Id = Guid.NewGuid(),
            Name = Name + " (Cópia)",
            IsDefault = false,
            CreatedAt = DateTime.Now
        };
        foreach (var app in Apps)
            clone.Apps.Add(app.Clone());
        return clone;
    }

    public override string ToString() => Name;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
