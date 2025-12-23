using System;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;
using EncryptedMessaging.Client.models;
using EncryptedMessaging.Client.services;
using EncryptedMessaging.Client.security;

namespace EncryptedMessaging.Client;

public class Application
{
    private readonly ApiClient _apiClient;
    private WebSocketClient? _wsClient;
    private int _currentUserId;
    private string _currentUsername = string.Empty;
    private byte[] _privateKey = Array.Empty<byte>();
    private Dictionary<string, string> _publicKeyCache = new();
    
    private volatile bool _inChatMode;
    private string _chatPartner = string.Empty;
    private byte[]? _currentSharedSecret;
    private readonly Queue<(string Sender, string Content, DateTime Time)> _pendingMessages = new();
    private readonly object _messageLock = new();
    
    // Message navigation state
    private int _currentMessageOffset = 20;
    private int _totalConversationMessages = 0;
    private int _highlightedMessageId = -1;

    public Application()
    {
        _apiClient = new ApiClient();
    }

    public async Task RunAsync()
    {
        Console.Clear();
        await ShowAnimatedWelcomeAsync();
        
        await TryRestoreSessionAsync();

        while (true)
        {
            if (_currentUserId == 0)
            {
                await ShowLoginMenuAsync();
            }
            else
            {
                await ShowMainMenuAsync();
            }
        }
    }
    
    private async Task TryRestoreSessionAsync()
    {
        var (session, privateKey) = SessionManager.LoadSession();
        if (session == null || privateKey == null)
            return;
            
        _apiClient.SetToken(session.Token);
        
        var isValid = await _apiClient.GetUsersAsync();
        if (isValid.Count == 0)
        {
            SessionManager.ClearSession();
            _apiClient.ClearToken();
            return;
        }
        
        _currentUserId = session.UserId;
        _currentUsername = session.Username;
        _privateKey = privateKey;
        
        AnsiConsole.MarkupLine($"[green]✓[/] Session restaurée. Bienvenue, [bold orange3]{_currentUsername}[/]!");
        await Task.Delay(1500);
        Console.Clear();
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
        AnsiConsole.MarkupLine("\n[dim]🔒 Chiffrement E2E | 🛡️ Sécurisé[/]\n");
        
        var serverOnline = await _apiClient.CheckHealthAsync();
        if (serverOnline)
        {
            AnsiConsole.MarkupLine("[green]✓ Serveur connecté[/]\n");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]✗ Serveur hors ligne - Vérifiez votre connexion[/]\n");
        }
        
        await Task.Delay(800);
    }

    private async Task ShowLoginMenuAsync()
    {
        var serverInfo = $"[dim]Serveur: {Config.ServerUrl}[/]";
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[yellow]═══[/] [orange3 bold]Bienvenue![/] [yellow]═══[/]\n{serverInfo}\n Que souhaitez-vous faire?")
                .PageSize(10)
                .HighlightStyle(new Style(foreground: Color.Yellow, decoration: Decoration.Bold))
                .AddChoices(new[] {
                    "🔐 Se connecter",
                    "📝 S'inscrire",
                    "🌐 Changer de serveur",
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
            case "🌐 Changer de serveur":
                await ChangeServerAsync();
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
    
    private async Task ChangeServerAsync()
    {
        Console.Clear();
        var panel = new Panel("[yellow]🌐 Changer de serveur[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
        
        AnsiConsole.MarkupLine($"[dim]Serveur actuel: {Config.ServerUrl}[/]");
        AnsiConsole.MarkupLine("[dim]Appuyez sur Entrée pour annuler[/]\n");
        
        var newUrl = AnsiConsole.Prompt(
            new TextPrompt<string>("[orange3]Nouvelle adresse du serveur:[/]")
                .AllowEmpty());
        
        if (string.IsNullOrWhiteSpace(newUrl))
        {
            Console.Clear();
            return;
        }
        
        Config.SetServerUrl(newUrl);
        _apiClient.UpdateBaseAddress();
        
        var isOnline = await _apiClient.CheckHealthAsync();
        if (isOnline)
        {
            AnsiConsole.MarkupLine($"\n[green]✓ Connecté à {Config.ServerUrl}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"\n[red]✗ Impossible de se connecter à {Config.ServerUrl}[/]");
        }
        
        await Task.Delay(2000);
        Console.Clear();
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

        AuthResponse? response = null;
        await AnsiConsole.Status()
            .StartAsync("[yellow]Connexion...[/]", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                response = await _apiClient.LoginAsync(username, password);
                await Task.Delay(500);
            });

        if (response != null)
        {
            _currentUserId = response.UserId;
            _currentUsername = response.Username;
            _privateKey = KeyDerivation.GetPrivateKey(username, password);
            _apiClient.SetToken(response.Token);
            
            SessionManager.SaveSession(
                response.UserId,
                response.Username,
                response.Token,
                response.RefreshToken,
                _privateKey
            );
            
            AnsiConsole.MarkupLine($"\n[green]✓[/] Bienvenue, [bold orange3]{_currentUsername}[/]!");
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

        if (password.Length < 6)
        {
            AnsiConsole.MarkupLine("\n[red]✗ Le mot de passe doit contenir au moins 6 caractères.[/]");
            await Task.Delay(2000);
            Console.Clear();
            return;
        }

        AuthResponse? response = null;
        await AnsiConsole.Status()
            .StartAsync("[yellow]Création du compte...[/]", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                var publicKey = KeyDerivation.GetPublicKeyBase64(username, password);
                response = await _apiClient.RegisterAsync(username, password, publicKey);
                await Task.Delay(500);
            });

        if (response != null)
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
        var unreadCount = await GetUnreadCountAsync();
        
        var choices = new List<string>
        {
            unreadCount > 0 ? $"💬 Ouvrir une conversation [yellow]🔔 {unreadCount}[/]" : "💬 Ouvrir une conversation",
            "👥 Liste des utilisateurs",
            "🚪 Se déconnecter",
            "❌ Quitter"
        };

        var notification = unreadCount > 0 ? $" [yellow bold]🔔 {unreadCount}[/]" : "";
        var title = $"[yellow]═══[/] [orange3 bold]{_currentUsername}[/] [yellow]═══[/]{notification}";

        var choice = await Task.Run(() => AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(title)
                .PageSize(10)
                .HighlightStyle(new Style(foreground: Color.Yellow, decoration: Decoration.Bold))
                .AddChoices(choices)));

        Console.Clear();

        var cleanChoice = choice.Split('[')[0].Trim();
        switch (cleanChoice)
        {
            case "💬 Ouvrir une conversation":
                await OpenConversationAsync();
                break;
            case "👥 Liste des utilisateurs":
                await ViewUsersAsync();
                break;
            case "🚪 Se déconnecter":
                _currentUserId = 0;
                _currentUsername = string.Empty;
                _privateKey = Array.Empty<byte>();
                _publicKeyCache.Clear();
                _apiClient.ClearToken();
                SessionManager.ClearSession();
                AnsiConsole.MarkupLine("[green]✓ Déconnexion réussie.[/]");
                await Task.Delay(1000);
                Console.Clear();
                await ShowAnimatedWelcomeAsync();
                break;
            case "❌ Quitter":
                Environment.Exit(0);
                break;
        }
    }

    private async Task<int> GetUnreadCountAsync()
    {
        var messages = await _apiClient.GetReceivedMessagesAsync();
        return messages.Count(m => !m.IsRead);
    }

    private async Task<Dictionary<string, int>> GetUnreadCountByUserAsync()
    {
        var messages = await _apiClient.GetReceivedMessagesAsync();
        return messages
            .Where(m => !m.IsRead)
            .GroupBy(m => m.SenderUsername)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<string?> GetUserPublicKeyAsync(string username)
    {
        if (_publicKeyCache.TryGetValue(username, out var cachedKey))
            return cachedKey;
            
        var user = await _apiClient.GetUserAsync(username);
        if (user == null)
            return null;
            
        _publicKeyCache[username] = user.PublicKey;
        return user.PublicKey;
    }

    private async Task ViewUsersAsync()
    {
        var users = await _apiClient.GetUsersAsync();
        var unreadByUser = await GetUnreadCountByUserAsync();

        if (users.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Aucun utilisateur enregistré.[/]");
            await Task.Delay(2000);
            Console.Clear();
            return;
        }

        var choices = users.Select(u =>
        {
            var date = u.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy");
            var isYou = u.Id == _currentUserId ? " [yellow](vous)[/]" : "";
            var unread = "";
            if (unreadByUser.TryGetValue(u.Username, out var count) && count > 0)
                unread = $" [yellow]🔔 {count}[/]";
            return $"{u.Username}{isYou}{unread} [dim]({date})[/]";
        }).ToList();
        choices.Add("↩️ Retour");

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]👥 Utilisateurs enregistrés[/]\n[dim]Tapez pour rechercher[/]")
                .PageSize(15)
                .MoreChoicesText("[dim]Utilisez ↑↓ pour voir plus[/]")
                .EnableSearch()
                .SearchPlaceholderText("[dim]Rechercher...[/]")
                .HighlightStyle(new Style(foreground: Color.Black, background: Color.Yellow, decoration: Decoration.Bold))
                .AddChoices(choices));

        if (selected == "↩️ Retour")
        {
            Console.Clear();
            return;
        }

        var selectedUsername = selected.Split(' ')[0].Split('[')[0].Trim();
        
        if (users.Any(u => u.Username == selectedUsername && u.Id == _currentUserId))
        {
            AnsiConsole.MarkupLine("[yellow]Vous ne pouvez pas vous envoyer un message.[/]");
            await Task.Delay(1500);
            Console.Clear();
            return;
        }
        
        Console.Clear();
        await StartInteractiveChatAsync(selectedUsername);
    }

    private async Task OpenConversationAsync()
    {
        var users = await _apiClient.GetUsersAsync();
        var otherUsers = users.Where(u => u.Id != _currentUserId).ToList();

        if (otherUsers.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Aucun autre utilisateur disponible.[/]");
            await Task.Delay(2000);
            Console.Clear();
            return;
        }

        var unreadByUser = await GetUnreadCountByUserAsync();
        
        var choices = otherUsers.Select(u => 
        {
            if (unreadByUser.TryGetValue(u.Username, out var count) && count > 0)
                return $"{u.Username} [yellow]🔔 {count}[/]";
            return u.Username;
        }).ToList();
        choices.Add("↩️ Retour");

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]💬 Choisir un utilisateur:[/]\n[dim]Tapez pour rechercher[/]")
                .PageSize(15)
                .MoreChoicesText("[dim]Utilisez ↑↓ pour voir plus[/]")
                .EnableSearch()
                .SearchPlaceholderText("[dim]Rechercher...[/]")
                .HighlightStyle(new Style(foreground: Color.Black, background: Color.Yellow, decoration: Decoration.Bold))
                .AddChoices(choices));

        if (selected == "↩️ Retour")
        {
            Console.Clear();
            return;
        }

        var selectedUsername = selected.Split('[')[0].Trim();
        await StartInteractiveChatAsync(selectedUsername);
    }

    private async Task StartInteractiveChatAsync(string partnerUsername)
    {
        _chatPartner = partnerUsername;
        _inChatMode = true;
        _currentMessageOffset = 20;
        _highlightedMessageId = -1;
        _totalConversationMessages = 0;
        
        var partnerPublicKey = await GetUserPublicKeyAsync(partnerUsername);
        if (partnerPublicKey == null)
        {
            AnsiConsole.MarkupLine("[red]✗ Utilisateur introuvable.[/]");
            await Task.Delay(2000);
            _inChatMode = false;
            Console.Clear();
            return;
        }

        var sharedSecret = KeyDerivation.DeriveSharedSecret(_privateKey, partnerPublicKey);
        _currentSharedSecret = sharedSecret;
        
        await ConnectWebSocketAsync();

        Console.Clear();
        DrawChatHeader(partnerUsername);
        
        await DisplayConversationHistoryAsync(partnerUsername, sharedSecret);
        
        AnsiConsole.MarkupLine("\n[dim]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/]");
        AnsiConsole.MarkupLine("[dim]Commandes: /up /latest /search /back[/]\n");

        var inputTask = Task.Run(() => ReadInputAsync());
        
        while (_inChatMode)
        {
            if (inputTask.IsCompleted)
            {
                var input = await inputTask;
                
                if (!string.IsNullOrEmpty(input))
                {
                    if (input.Equals("/back", StringComparison.OrdinalIgnoreCase))
                    {
                        _inChatMode = false;
                        break;
                    }
                    else if (input.Equals("/up", StringComparison.OrdinalIgnoreCase))
                    {
                        _currentMessageOffset += 20;
                        _highlightedMessageId = -1;
                        await ReloadConversationViewAsync(partnerUsername, sharedSecret);
                    }
                    else if (input.Equals("/latest", StringComparison.OrdinalIgnoreCase) || input.Equals("/down", StringComparison.OrdinalIgnoreCase))
                    {
                        _currentMessageOffset = 20;
                        _highlightedMessageId = -1;
                        await ReloadConversationViewAsync(partnerUsername, sharedSecret);
                    }
                    else if (input.Equals("/search", StringComparison.OrdinalIgnoreCase))
                    {
                        await InteractiveSearchAsync(partnerUsername, sharedSecret);
                        Console.Clear();
                        DrawChatHeader(partnerUsername);
                        await ReloadConversationViewAsync(partnerUsername, sharedSecret);
                        AnsiConsole.MarkupLine("\n[dim]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/]");
                        AnsiConsole.MarkupLine("[dim]Commandes: /up /latest /search /back[/]\n");
                    }
                    else
                    {
                        var encryptedContent = KeyDerivation.Encrypt(input, sharedSecret);
                        var message = await _apiClient.SendMessageAsync(partnerUsername, encryptedContent);
                        
                        if (message == null)
                        {
                            AnsiConsole.MarkupLine("[red]✗ Échec de l'envoi.[/]");
                        }
                        else
                        {
                            _highlightedMessageId = -1;
                        }
                    }
                }
                
                if (_inChatMode)
                {
                    inputTask = Task.Run(() => ReadInputAsync());
                }
            }
            
            DisplayPendingMessages();
            await Task.Delay(100);
        }

        _currentSharedSecret = null;
        await DisconnectWebSocketAsync();
        _chatPartner = string.Empty;
        Console.Clear();
    }
    
    private string? ReadInputAsync()
    {
        AnsiConsole.Markup($"[orange3]{_currentUsername}>[/] ");
        return Console.ReadLine();
    }

    private void DrawChatHeader(string partnerUsername)
    {
        var panel = new Panel($"[yellow]💬 Conversation avec [orange3 bold]{partnerUsername}[/][/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Yellow)
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private async Task DisplayConversationHistoryAsync(string partnerUsername, byte[] sharedSecret)
    {
        var received = await _apiClient.GetReceivedMessagesAsync();
        var sent = await _apiClient.GetSentMessagesAsync();

        var allMessages = received
            .Where(m => m.SenderUsername == partnerUsername)
            .Select(m => (m.Id, Sender: m.SenderUsername, m.EncryptedContent, m.SentAt, IsMe: false))
            .Concat(sent
                .Where(m => m.ReceiverUsername == partnerUsername)
                .Select(m => (m.Id, Sender: _currentUsername, m.EncryptedContent, m.SentAt, IsMe: true)))
            .OrderBy(m => m.SentAt)
            .ToList();

        _totalConversationMessages = allMessages.Count;
        
        var conversation = allMessages
            .TakeLast(_currentMessageOffset)
            .ToList();

        if (_totalConversationMessages > 0)
        {
            var start = Math.Max(1, _totalConversationMessages - conversation.Count + 1);
            var end = _totalConversationMessages;
            AnsiConsole.MarkupLine($"[dim]📜 Affichage: messages {start}-{end} sur {_totalConversationMessages} messages[/]\n");
        }

        if (conversation.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]Aucun message dans cette conversation.[/]");
            return;
        }

        foreach (var msg in conversation)
        {
            var decrypted = KeyDerivation.Decrypt(msg.EncryptedContent, sharedSecret);
            var time = msg.SentAt.ToLocalTime().ToString("HH:mm");
            
            var isHighlighted = msg.Id == _highlightedMessageId;
            var prefix = isHighlighted ? "→ " : "  ";
            var highlight = isHighlighted ? " on yellow" : "";
            
            if (msg.IsMe)
            {
                AnsiConsole.MarkupLine($"{prefix}[dim{highlight}]{time}[/] [orange3{highlight}]{_currentUsername}:[/] [{(isHighlighted ? "black on yellow" : "default")}]{Markup.Escape(decrypted)}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"{prefix}[dim{highlight}]{time}[/] [green{highlight}]{msg.Sender}:[/] [{(isHighlighted ? "black on yellow" : "default")}]{Markup.Escape(decrypted)}[/]");
            }
        }

        var unreadFromPartner = received
            .Where(m => m.SenderUsername == partnerUsername && !m.IsRead)
            .ToList();
        
        foreach (var msg in unreadFromPartner)
        {
            await _apiClient.MarkMessageAsReadAsync(msg.Id);
        }
    }

    private void DisplayPendingMessages()
    {
        lock (_messageLock)
        {
            while (_pendingMessages.Count > 0)
            {
                var msg = _pendingMessages.Dequeue();
                var time = msg.Time.ToLocalTime().ToString("HH:mm");
                Console.WriteLine();
                AnsiConsole.MarkupLine($"[dim]{time}[/] [green]{msg.Sender}:[/] {Markup.Escape(msg.Content)}");
                AnsiConsole.Markup($"[orange3]{_currentUsername}>[/] ");
            }
        }
    }

    private async Task ConnectWebSocketAsync()
    {
        var token = _apiClient.GetToken();
        if (string.IsNullOrEmpty(token))
            return;

        _wsClient = new WebSocketClient();
        _wsClient.OnMessageReceived += HandleWebSocketMessage;
        await _wsClient.ConnectAsync(token);
    }

    private async Task DisconnectWebSocketAsync()
    {
        if (_wsClient != null)
        {
            _wsClient.OnMessageReceived -= HandleWebSocketMessage;
            await _wsClient.DisconnectAsync();
            _wsClient.Dispose();
            _wsClient = null;
        }
    }

    private void HandleWebSocketMessage(string messageType, System.Text.Json.JsonElement payload)
    {
        if (!_inChatMode || _currentSharedSecret == null)
            return;

        if (messageType == "new_message")
        {
            try
            {
                string? senderUsername = null;
                if (payload.TryGetProperty("SenderUsername", out var su))
                    senderUsername = su.GetString();
                else if (payload.TryGetProperty("senderUsername", out su))
                    senderUsername = su.GetString();
                
                if (senderUsername == _chatPartner)
                {
                    var sharedSecret = _currentSharedSecret;
                    _ = Task.Run(async () =>
                    {
                        var messages = await _apiClient.GetReceivedMessagesAsync();
                        var latestMsg = messages
                            .Where(m => m.SenderUsername == _chatPartner && !m.IsRead)
                            .OrderByDescending(m => m.SentAt)
                            .FirstOrDefault();

                        if (latestMsg != null)
                        {
                            var decrypted = KeyDerivation.Decrypt(latestMsg.EncryptedContent, sharedSecret);
                            
                            lock (_messageLock)
                            {
                                _pendingMessages.Enqueue((latestMsg.SenderUsername, decrypted, latestMsg.SentAt));
                            }
                            
                            await _apiClient.MarkMessageAsReadAsync(latestMsg.Id);
                        }
                    });
                }
            }
            catch
            {
            }
        }
    }

    private async Task ReloadConversationViewAsync(string partnerUsername, byte[] sharedSecret)
    {
        var atBeginning = false;
        
        if (_currentMessageOffset > _totalConversationMessages)
        {
            _currentMessageOffset = _totalConversationMessages;
            atBeginning = true;
        }
        
        Console.Clear();
        DrawChatHeader(partnerUsername);
        
        if (atBeginning && _totalConversationMessages > 0)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Début de la conversation atteint[/]\n");
        }
        
        await DisplayConversationHistoryAsync(partnerUsername, sharedSecret);
        AnsiConsole.MarkupLine("\n[dim]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/]");
        AnsiConsole.MarkupLine("[dim]Commandes: /up /latest /search /back[/]\n");
    }

    private async Task InteractiveSearchAsync(string partnerUsername, byte[] sharedSecret)
    {
        while (true)
        {
            Console.Clear();
            AnsiConsole.MarkupLine("[yellow]🔍 Rechercher dans la conversation[/]\n");
            AnsiConsole.Markup("Entrez votre terme de recherche (ou Entrée pour annuler): ");
            var searchTerm = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(searchTerm))
                return;
            
            var results = await FindMessagesAsync(partnerUsername, sharedSecret, searchTerm);
            
            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Aucun résultat trouvé.[/]");
                await Task.Delay(1500);
                continue;
            }
            
            var choices = results.Select(r => 
                $"[dim]{r.Time}[/] [{(r.IsMe ? "orange3" : "green")}]{r.Sender}:[/] {r.Preview}").ToList();
            choices.Add("🔄 Nouvelle recherche");
            choices.Add("↩️ Retour au chat");
            
            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[yellow]🔍 Résultats pour \"{Markup.Escape(searchTerm)}\" ({results.Count} trouvés)[/]")
                    .PageSize(15)
                    .EnableSearch()
                    .SearchPlaceholderText("[dim]Filtrer les résultats...[/]")
                    .HighlightStyle(new Style(foreground: Color.Black, background: Color.Yellow, decoration: Decoration.Bold))
                    .AddChoices(choices));
            
            if (selected == "↩️ Retour au chat")
                return;
            
            if (selected == "🔄 Nouvelle recherche")
                continue;
            
            var selectedIndex = choices.IndexOf(selected);
            var targetMessage = results[selectedIndex];
            
            _highlightedMessageId = targetMessage.MessageId;
            await JumpToMessageAsync(partnerUsername, sharedSecret, targetMessage.MessageIndex);
            return;
        }
    }

    private async Task<List<(int MessageId, int MessageIndex, string Sender, string Time, string Preview, bool IsMe)>> 
        FindMessagesAsync(string partnerUsername, byte[] sharedSecret, string searchTerm)
    {
        var received = await _apiClient.GetReceivedMessagesAsync();
        var sent = await _apiClient.GetSentMessagesAsync();

        var allMessages = received
            .Where(m => m.SenderUsername == partnerUsername)
            .Select(m => (m.Id, Sender: m.SenderUsername, m.EncryptedContent, m.SentAt, IsMe: false))
            .Concat(sent
                .Where(m => m.ReceiverUsername == partnerUsername)
                .Select(m => (m.Id, Sender: _currentUsername, m.EncryptedContent, m.SentAt, IsMe: true)))
            .OrderBy(m => m.SentAt)
            .ToList();

        var results = new List<(int MessageId, int MessageIndex, string Sender, string Time, string Preview, bool IsMe)>();
        
        for (int i = 0; i < allMessages.Count; i++)
        {
            var msg = allMessages[i];
            var decrypted = KeyDerivation.Decrypt(msg.EncryptedContent, sharedSecret);
            
            if (decrypted.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                var time = msg.SentAt.ToLocalTime().ToString("dd/MM HH:mm");
                var preview = decrypted.Length > 60 ? decrypted.Substring(0, 60) + "..." : decrypted;
                results.Add((msg.Id, i, msg.Sender, time, preview, msg.IsMe));
            }
        }
        
        return results;
    }

    private async Task JumpToMessageAsync(string partnerUsername, byte[] sharedSecret, int targetMessageIndex)
    {
        var before = 10;
        var after = 9;
        
        var startIndex = Math.Max(0, targetMessageIndex - before);
        var messagesToShow = Math.Min(_totalConversationMessages, targetMessageIndex + after + 1) - startIndex;
        
        _currentMessageOffset = _totalConversationMessages - startIndex;
        
        await ReloadConversationViewAsync(partnerUsername, sharedSecret);
    }
}
