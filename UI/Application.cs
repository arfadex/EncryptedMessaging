using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using EncryptedMessaging.Models;
using EncryptedMessaging.Services;

namespace EncryptedMessaging.UI;

public class Application
{
    private readonly AuthService _authService;
    private readonly MessageService _messageService;
    private User? _currentUser;
    private const string ADMIN_USERNAME = "admin";
    private const string ADMIN_PASSWORD = "admin";
    private CancellationTokenSource? _notificationCancellation;

    public Application()
    {
        _authService = new AuthService();
        _messageService = new MessageService();
    }

    private async Task<int> GetUnreadMessageCountAsync()
    {
        if (_currentUser == null) return 0;
        var messages = await _messageService.GetReceivedMessagesAsync(_currentUser.Id);
        return messages.Count(m => !m.IsRead);
    }

    public async Task RunAsync()
    {
        Console.Clear();
        await ShowAnimatedWelcomeAsync();

        while (true)
        {
            if (_currentUser == null)
            {
                await ShowLoginMenuAsync();
            }
            else
            {
                await ShowMainMenuAsync();
            }
        }
    }

    private async Task ShowAnimatedWelcomeAsync()
    {
        await AnsiConsole.Status()
            .StartAsync("[yellow]Chargement...[/]", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Star);
                ctx.SpinnerStyle(Style.Parse("yellow"));
                await Task.Delay(800);
            });

        Console.Clear();
        
        var gradient = new string[]
        {
            "[yellow]███████╗███╗   ██╗ ██████╗██████╗ ██╗   ██╗██████╗ ████████╗███████╗██████╗ [/]",
            "[orange3]██╔════╝████╗  ██║██╔════╝██╔══██╗╚██╗ ██╔╝██╔══██╗╚══██╔══╝██╔════╝██╔══██╗[/]",
            "[olive]█████╗  ██╔██╗ ██║██║     ██████╔╝ ╚████╔╝ ██████╔╝   ██║   █████╗  ██║  ██║[/]",
            "[green]██╔══╝  ██║╚██╗██║██║     ██╔══██╗  ╚██╔╝  ██╔═══╝    ██║   ██╔══╝  ██║  ██║[/]",
            "[yellow]███████╗██║ ╚████║╚██████╗██║  ██║   ██║   ██║        ██║   ███████╗██████╔╝[/]",
            "[orange3]╚══════╝╚═╝  ╚═══╝ ╚═════╝╚═╝  ╚═╝   ╚═╝   ╚═╝        ╚═╝   ╚══════╝╚═════╝ [/]"
        };

        foreach (var line in gradient)
        {
            AnsiConsole.MarkupLine(line);
            await Task.Delay(100);
        }

        var rule = new Rule("[yellow]Système de Messagerie Chiffré[/]")
        {
            Style = Style.Parse("olive")
        };
        AnsiConsole.Write(rule);
        
        await Task.Delay(500);
        AnsiConsole.MarkupLine("\n[dim]🔒 Chiffrement AES-256 | 🔑 Hash PBKDF2 | 🛡️ Sécurisé[/]\n");
        await Task.Delay(800);
    }

    private async Task ShowLoginMenuAsync()
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]═══[/] [orange3 bold]Bienvenue![/] [yellow]═══[/]\n Que souhaitez-vous faire?")
                .PageSize(10)
                .HighlightStyle(new Style(foreground: Color.Yellow, decoration: Decoration.Bold))
                .AddChoices(new[] {
                    "🔐 Se connecter",
                    "📝 S'inscrire",
                    "❌ Quitter"
                }));

        switch (choice)
        {
            case "🔐 Se connecter":
                await LoginAsync();
                break;
            case "📝 S'inscrire":
                await RegisterAsync();
                break;
            case "❌ Quitter":
                await AnsiConsole.Status()
                    .StartAsync("[yellow]Fermeture...[/]", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        await Task.Delay(500);
                    });
                AnsiConsole.MarkupLine("\n[green]👋 Au revoir![/]\n");
                Environment.Exit(0);
                break;
        }
    }

    private async Task LoginAsync()
    {
        Console.Clear();
        var panel = new Panel("[yellow]🔐 Connexion[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        var username = AnsiConsole.Ask<string>("[orange3]Nom d'utilisateur:[/]");
        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("[orange3]Mot de passe:[/]")
                .Secret());

        await AnsiConsole.Status()
            .StartAsync("[yellow]Vérification...[/]", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(500);

                if (username.ToLower() == ADMIN_USERNAME && password == ADMIN_PASSWORD)
                {
                    _currentUser = new User 
                    { 
                        Id = -1, 
                        Username = "admin", 
                        PasswordHash = "",
                        CreatedAt = DateTime.UtcNow 
                    };
                }
                else
                {
                    _currentUser = await _authService.LoginAsync(username, password);
                }
            });

        if (_currentUser != null)
        {
            if (_currentUser.Id == -1)
            {
                AnsiConsole.MarkupLine("\n[green]✓[/] [bold orange3]Bienvenue, Administrateur![/] 👑");
            }
            else
            {
                // Check for unread messages
                int unreadCount = await GetUnreadMessageCountAsync();
                
                if (unreadCount > 0)
                {
                    var notifPanel = new Panel(
                        new Markup($"[yellow bold]🔔 Vous avez {unreadCount} nouveau(x) message(s)![/]"))
                    {
                        Border = BoxBorder.Rounded,
                        BorderStyle = new Style(foreground: Color.Yellow)
                    };
                    AnsiConsole.Write(notifPanel);
                }
                
                AnsiConsole.MarkupLine($"\n[green]✓[/] Bienvenue, [bold orange3]{_currentUser.Username}[/]!");
                
                // Start live notification system
                _notificationCancellation = new CancellationTokenSource();
            }
            await Task.Delay(2000);
        }
        else
        {
            AnsiConsole.MarkupLine("\n[red]✗ Identifiants incorrects.[/]");
            await Task.Delay(2000);
        }

        Console.Clear();
    }

    private async Task RegisterAsync()
    {
        Console.Clear();
        var panel = new Panel("[yellow]📝 Inscription[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        var username = AnsiConsole.Ask<string>("[orange3]Choisir un nom d'utilisateur:[/]");
        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("[orange3]Choisir un mot de passe (min. 6 caractères):[/]")
                .Secret());
        var confirmPassword = AnsiConsole.Prompt(
            new TextPrompt<string>("[orange3]Confirmer le mot de passe:[/]")
                .Secret());

        if (password != confirmPassword)
        {
            AnsiConsole.MarkupLine("\n[red]✗ Les mots de passe ne correspondent pas.[/]");
            await Task.Delay(2000);
            Console.Clear();
            return;
        }

        User? user = null;
        await AnsiConsole.Status()
            .StartAsync("[yellow]Création du compte...[/]", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(500);
                user = await _authService.RegisterAsync(username, password);
            });

        if (user != null)
        {
            AnsiConsole.MarkupLine($"\n[green]✓ Compte créé avec succès![/] Vous pouvez maintenant vous connecter.");
            await Task.Delay(2000);
        }
        else
        {
            AnsiConsole.MarkupLine("\n[red]✗ Erreur: Ce nom d'utilisateur existe déjà ou est invalide.[/]");
            await Task.Delay(2000);
        }

        Console.Clear();
    }

    private async Task ShowMainMenuAsync()
    {
        int unreadCount = await GetUnreadMessageCountAsync();
        string notification = unreadCount > 0 ? $" [yellow bold]🔔 {unreadCount}[/]" : "";
        
        var choices = new System.Collections.Generic.List<string>();

        // Admin menu
        if (_currentUser!.Id == -1)
        {
            choices.AddRange(new[] {
                "👥 Gérer les utilisateurs",
                "➕ Ajouter un utilisateur",
                "📊 Statistiques",
                "🔄 Rafraîchir",
                "🚪 Se déconnecter"
            });
        }
        else
        {
            // Regular user menu
            choices.AddRange(new[] {
                "📨 Envoyer un message",
                $"📥 Messages reçus{(unreadCount > 0 ? $" [yellow]({unreadCount})[/]" : "")}",
                "📤 Messages envoyés",
                "👥 Liste des utilisateurs",
                "🔄 Rafraîchir",
                "🚪 Se déconnecter"
            });
        }

        var title = _currentUser.Id == -1 
            ? $"[yellow]═══[/] [red bold]ADMIN[/] [yellow]═══[/]{notification}"
            : $"[yellow]═══[/] [orange3 bold]{_currentUser.Username}[/] [yellow]═══[/]{notification}";

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(title)
                .PageSize(10)
                .HighlightStyle(new Style(foreground: Color.Yellow, decoration: Decoration.Bold))
                .AddChoices(choices));

        Console.Clear();

        if (_currentUser.Id == -1)
        {
            // Admin actions
            switch (choice)
            {
                case "👥 Gérer les utilisateurs":
                    await ManageUsersAsync();
                    break;
                case "➕ Ajouter un utilisateur":
                    await AddUserAsync();
                    break;
                case "📊 Statistiques":
                    await ViewStatisticsAsync();
                    break;
                case "🔄 Rafraîchir":
                    AnsiConsole.MarkupLine("[yellow]🔄 Actualisation...[/]");
                    await Task.Delay(500);
                    Console.Clear();
                    break;
                case "🚪 Se déconnecter":
                    _currentUser = null;
                    AnsiConsole.MarkupLine("[green]✓ Déconnexion réussie.[/]");
                    await Task.Delay(1000);
                    Console.Clear();
                    await ShowAnimatedWelcomeAsync();
                    break;
            }
        }
        else
        {
            // Regular user actions
            var cleanChoice = choice.Split('[')[0].Trim();
            switch (cleanChoice)
            {
                case "📨 Envoyer un message":
                    await SendMessageAsync();
                    break;
                case "📥 Messages reçus":
                    await ViewReceivedMessagesAsync();
                    break;
                case "📤 Messages envoyés":
                    await ViewSentMessagesAsync();
                    break;
                case "👥 Liste des utilisateurs":
                    await ViewUsersAsync();
                    break;
                case "🔄 Rafraîchir":
                    AnsiConsole.MarkupLine("[yellow]🔄 Actualisation...[/]");
                    await Task.Delay(500);
                    Console.Clear();
                    break;
                case "🚪 Se déconnecter":
                    _notificationCancellation?.Cancel();
                    _currentUser = null;
                    AnsiConsole.MarkupLine("[green]✓ Déconnexion réussie.[/]");
                    await Task.Delay(1000);
                    Console.Clear();
                    await ShowAnimatedWelcomeAsync();
                    break;
            }
        }
    }

    private async Task SendMessageAsync()
    {
        var panel = new Panel("[yellow]📨 Envoyer un message[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        var receiver = AnsiConsole.Ask<string>("[orange3]Destinataire (nom d'utilisateur):[/]");
        var content = AnsiConsole.Ask<string>("[orange3]Message:[/]");

        Message? message = null;
        await AnsiConsole.Status()
            .StartAsync("[yellow]Envoi en cours...[/]", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                message = await _messageService.SendMessageAsync(_currentUser!.Id, receiver, content);
                await Task.Delay(500);
            });

        if (message != null)
        {
            AnsiConsole.MarkupLine($"\n[green]✓ Message envoyé à[/] [orange3 bold]{receiver}[/]! 🚀");
            AnsiConsole.MarkupLine("[dim]Le destinataire peut rafraîchir son menu pour voir le message.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("\n[red]✗ Utilisateur introuvable.[/]");
        }

        await Task.Delay(2500);
        Console.Clear();
    }

    private async Task ViewReceivedMessagesAsync()
    {
        var messages = await _messageService.GetReceivedMessagesAsync(_currentUser!.Id);

        var panel = new Panel("[yellow]📥 Messages reçus[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (messages.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]📭 Aucun message.[/]");
            AnsiConsole.WriteLine("\n[dim]Appuyez sur Entrée pour continuer...[/]");
            Console.ReadLine();
            Console.Clear();
            return;
        }

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.BorderColor(Color.Yellow);
        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn("De");
        table.AddColumn("Message");
        table.AddColumn("Date");
        table.AddColumn(new TableColumn("Statut").Centered());

        foreach (var msg in messages)
        {
            var statusIcon = msg.IsRead ? "[dim]✓ Lu[/]" : "[yellow bold]● Nouveau[/]";
            table.AddRow(
                msg.Id.ToString(),
                $"[orange3]{msg.SenderUsername}[/]",
                msg.DecryptedContent.Length > 50 ? msg.DecryptedContent.Substring(0, 47) + "..." : msg.DecryptedContent,
                msg.SentAt.ToLocalTime().ToString("dd/MM HH:mm"),
                statusIcon
            );
        }

        AnsiConsole.Write(table);

        // Auto-mark all unread messages as read
        var unreadMessages = messages.Where(m => !m.IsRead).ToList();
        if (unreadMessages.Any())
        {
            foreach (var msg in unreadMessages)
            {
                await _messageService.MarkAsReadAsync(msg.Id, _currentUser.Id);
            }
            AnsiConsole.MarkupLine($"\n[green]✓ {unreadMessages.Count} message(s) marqué(s) comme lu(s).[/]");
        }

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("\n[yellow]Actions:[/]")
                .HighlightStyle(new Style(foreground: Color.Yellow, decoration: Decoration.Bold))
                .AddChoices(new[] { "🔄 Rafraîchir", "↩️ Retour au menu" }));

        if (action == "🔄 Rafraîchir")
        {
            Console.Clear();
            await ViewReceivedMessagesAsync();
            return;
        }

        Console.Clear();
    }

    private async Task ViewSentMessagesAsync()
    {
        var messages = await _messageService.GetSentMessagesAsync(_currentUser!.Id);

        var panel = new Panel("[yellow]📤 Messages envoyés[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (messages.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]📭 Aucun message envoyé.[/]");
            AnsiConsole.WriteLine("\n[dim]Appuyez sur Entrée pour continuer...[/]");
            Console.ReadLine();
            Console.Clear();
            return;
        }

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.BorderColor(Color.Yellow);
        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn("À");
        table.AddColumn("Message");
        table.AddColumn("Date");

        foreach (var msg in messages)
        {
            table.AddRow(
                msg.Id.ToString(),
                $"[orange3]{msg.ReceiverUsername}[/]",
                msg.DecryptedContent.Length > 50 ? msg.DecryptedContent.Substring(0, 47) + "..." : msg.DecryptedContent,
                msg.SentAt.ToLocalTime().ToString("dd/MM HH:mm")
            );
        }

        AnsiConsole.Write(table);

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("\n[yellow]Actions:[/]")
                .HighlightStyle(new Style(foreground: Color.Yellow, decoration: Decoration.Bold))
                .AddChoices(new[] { "✏️ Modifier un message", "🗑️ Supprimer un message", "🔄 Rafraîchir", "↩️ Retour" }));

        if (action == "✏️ Modifier un message")
        {
            var msgId = AnsiConsole.Ask<int>("[orange3]ID du message:[/]");
            var newContent = AnsiConsole.Ask<string>("[orange3]Nouveau contenu:[/]");
            var success = await _messageService.UpdateMessageAsync(msgId, _currentUser.Id, newContent);
            AnsiConsole.MarkupLine(success ? "[green]✓ Message modifié.[/]" : "[red]✗ Échec.[/]");
            await Task.Delay(1500);
            Console.Clear();
            await ViewSentMessagesAsync();
        }
        else if (action == "🗑️ Supprimer un message")
        {
            var msgId = AnsiConsole.Ask<int>("[orange3]ID du message:[/]");
            var success = await _messageService.DeleteMessageAsync(msgId, _currentUser.Id);
            AnsiConsole.MarkupLine(success ? "[green]✓ Message supprimé.[/]" : "[red]✗ Échec.[/]");
            await Task.Delay(1500);
            Console.Clear();
            await ViewSentMessagesAsync();
        }
        else if (action == "🔄 Rafraîchir")
        {
            Console.Clear();
            await ViewSentMessagesAsync();
        }
        else
        {
            Console.Clear();
        }
    }

    private async Task ViewUsersAsync()
    {
        var users = await _messageService.GetAllUsersAsync();

        var panel = new Panel("[yellow]👥 Utilisateurs enregistrés[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.BorderColor(Color.Yellow);
        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn("Nom d'utilisateur");
        table.AddColumn("Date d'inscription");

        foreach (var user in users)
        {
            string username;
            if (user.Id == _currentUser!.Id)
            {
                username = $"[orange3 bold]{user.Username}[/] [yellow](vous)[/]";
            }
            else
            {
                username = user.Username;
            }

            table.AddRow(
                user.Id.ToString(),
                username,
                user.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy")
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine("\n[dim]Appuyez sur Entrée pour continuer...[/]");
        Console.ReadLine();
        Console.Clear();
    }

    // Admin functions
    private async Task ManageUsersAsync()
    {
        var users = await _messageService.GetAllUsersAsync();
        var userRepo = new Data.UserRepository();

        var panel = new Panel("[red bold]👥 Gestion des utilisateurs (ADMIN)[/]")
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(foreground: Color.Red)
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.BorderColor(Color.Red);
        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn("Nom d'utilisateur");
        table.AddColumn("Date d'inscription");
        table.AddColumn(new TableColumn("Créé par").Centered());

        foreach (var user in users)
        {
            var createdBy = user.PasswordHash.StartsWith("ADMIN_") ? "[red]Admin[/]" : "[dim]User[/]";
            table.AddRow(
                user.Id.ToString(),
                user.Username,
                user.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                createdBy
            );
        }

        AnsiConsole.Write(table);

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("\n[yellow]Actions:[/]")
                .HighlightStyle(new Style(foreground: Color.Red, decoration: Decoration.Bold))
                .AddChoices(new[] { 
                    "✏️ Modifier un utilisateur (créé par admin)", 
                    "🗑️ Supprimer un utilisateur (créé par admin)", 
                    "🔄 Rafraîchir",
                    "↩️ Retour" 
                }));

        if (action == "✏️ Modifier un utilisateur (créé par admin)")
        {
            var userId = AnsiConsole.Ask<int>("[orange3]ID de l'utilisateur:[/]");
            var userToModify = users.FirstOrDefault(u => u.Id == userId);
            
            if (userToModify == null)
            {
                AnsiConsole.MarkupLine("[red]✗ Utilisateur introuvable.[/]");
                await Task.Delay(2000);
                Console.Clear();
                return;
            }

            if (!userToModify.PasswordHash.StartsWith("ADMIN_"))
            {
                AnsiConsole.MarkupLine("[red]✗ Vous ne pouvez modifier que les utilisateurs créés par l'admin.[/]");
                await Task.Delay(2000);
                Console.Clear();
                return;
            }

            var newUsername = AnsiConsole.Confirm("Modifier le nom d'utilisateur?") 
                ? AnsiConsole.Ask<string>($"[orange3]Nouveau nom (actuel: {userToModify.Username}):[/]")
                : userToModify.Username;
            
            var changePassword = AnsiConsole.Confirm("Modifier le mot de passe?");
            var newHash = userToModify.PasswordHash;
            
            if (changePassword)
            {
                var newPassword = AnsiConsole.Prompt(
                    new TextPrompt<string>("[orange3]Nouveau mot de passe (min. 6 caractères):[/]")
                        .Secret());
                
                if (newPassword.Length >= 6)
                {
                    newHash = "ADMIN_" + Security.PasswordHasher.HashPassword(newPassword);
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]✗ Mot de passe trop court. Modification annulée.[/]");
                    await Task.Delay(2000);
                    Console.Clear();
                    return;
                }
            }

            var success = await userRepo.UpdateUserAsync(userId, newUsername, newHash);
            
            if (success)
            {
                AnsiConsole.MarkupLine($"[green]✓ Utilisateur {newUsername} modifié avec succès![/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Erreur lors de la modification (nom d'utilisateur existe déjà?).[/]");
            }
            
            await Task.Delay(2000);
        }
        else if (action == "🗑️ Supprimer un utilisateur (créé par admin)")
        {
            var userId = AnsiConsole.Ask<int>("[orange3]ID de l'utilisateur à supprimer:[/]");
            
            var userToDelete = users.FirstOrDefault(u => u.Id == userId);
            if (userToDelete == null)
            {
                AnsiConsole.MarkupLine("[red]✗ Utilisateur introuvable.[/]");
                await Task.Delay(2000);
                Console.Clear();
                return;
            }

            if (!userToDelete.PasswordHash.StartsWith("ADMIN_"))
            {
                AnsiConsole.MarkupLine("[red]✗ Vous ne pouvez supprimer que les utilisateurs créés par l'admin.[/]");
                await Task.Delay(2000);
                Console.Clear();
                return;
            }

            if (AnsiConsole.Confirm($"[red]Confirmer la suppression de {userToDelete.Username}?[/]"))
            {
                var success = await userRepo.DeleteUserAsync(userId);
                
                if (success)
                {
                    AnsiConsole.MarkupLine($"[green]✓ Utilisateur {userToDelete.Username} supprimé avec succès.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]✗ Erreur lors de la suppression.[/]");
                }
                await Task.Delay(2000);
            }
        }
        else if (action == "🔄 Rafraîchir")
        {
            Console.Clear();
            await ManageUsersAsync();
            return;
        }

        Console.Clear();
    }

    private async Task AddUserAsync()
    {
        var panel = new Panel("[red bold]➕ Ajouter un utilisateur (ADMIN)[/]")
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(foreground: Color.Red)
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        var username = AnsiConsole.Ask<string>("[orange3]Nom d'utilisateur:[/]");
        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("[orange3]Mot de passe (min. 6 caractères):[/]")
                .Secret());

        if (password.Length < 6)
        {
            AnsiConsole.MarkupLine("\n[red]✗ Le mot de passe doit contenir au moins 6 caractères.[/]");
            await Task.Delay(2000);
            Console.Clear();
            return;
        }

        // Prefix hash with "ADMIN_" to mark as admin-created
        var passwordHash = "ADMIN_" + Security.PasswordHasher.HashPassword(password);
        var userRepo = new Data.UserRepository();
        
        User? user = null;
        await AnsiConsole.Status()
            .StartAsync("[yellow]Création de l'utilisateur...[/]", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                user = await userRepo.CreateUserAsync(username, passwordHash);
                await Task.Delay(500);
            });

        if (user != null)
        {
            AnsiConsole.MarkupLine($"\n[green]✓ Utilisateur {username} créé avec succès![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("\n[red]✗ Erreur: Ce nom d'utilisateur existe déjà.[/]");
        }

        await Task.Delay(2000);
        Console.Clear();
    }

    private async Task ViewStatisticsAsync()
    {
        var users = await _messageService.GetAllUsersAsync();
        var adminCreatedUsers = users.Count(u => u.PasswordHash.StartsWith("ADMIN_"));
        var userCreatedUsers = users.Count - adminCreatedUsers;

        var panel = new Panel("[red bold]📊 Statistiques du système (ADMIN)[/]")
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(foreground: Color.Red)
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        var statsPanel = new Panel(
            new Markup(
                $"[orange3]👥 Utilisateurs totaux:[/] [white bold]{users.Count}[/]\n" +
                $"[red]├─ Créés par admin:[/] [white]{adminCreatedUsers}[/]\n" +
                $"[green]└─ Auto-inscrits:[/] [white]{userCreatedUsers}[/]\n\n" +
                $"[green]✓ Système opérationnel[/]"
            ))
        {
            Header = new PanelHeader("📈 Tableau de bord", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        };

        AnsiConsole.Write(statsPanel);

        AnsiConsole.WriteLine("\n\n[yellow]Liste complète des utilisateurs:[/]");
        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.BorderColor(Color.Red);
        table.AddColumn(new TableColumn("ID").Centered());
        table.AddColumn("Utilisateur");
        table.AddColumn("Inscrit le");
        table.AddColumn(new TableColumn("Type").Centered());

        foreach (var user in users)
        {
            var userType = user.PasswordHash.StartsWith("ADMIN_") ? "[red]Admin[/]" : "[green]User[/]";
            table.AddRow(
                user.Id.ToString(),
                user.Username,
                user.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"),
                userType
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine("\n[dim]Appuyez sur Entrée pour continuer...[/]");
        Console.ReadLine();
        Console.Clear();
    }
}