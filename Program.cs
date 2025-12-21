using System;
using System.Threading.Tasks;
using EncryptedMessaging.UI;
using EncryptedMessaging.Data;

namespace EncryptedMessaging;

class Program
{
    static async Task Main(string[] args)
    {
        // Initialize database
        DatabaseInitializer.Initialize();
        
        // Start the application
        var app = new Application();
        await app.RunAsync();
    }
}