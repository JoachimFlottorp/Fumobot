﻿using Autofac;
using Fumo.Database;
using Fumo.Extensions.AutoFacInstallers;
using Fumo.Interfaces;
using Fumo.Models;
using Fumo.ThirdParty.ThreeLetterAPI;
using Fumo.ThirdParty.ThreeLetterAPI.Instructions;
using Fumo.ThirdParty.ThreeLetterAPI.Response;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Fumo;

internal class Program
{
    static async Task Main(string[] args)
    {
        var cwd = Directory.GetCurrentDirectory();
        var configPath = args.Length > 0 ? args[0] : "config.json";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(cwd)
            .AddJsonFile(configPath, optional: false, reloadOnChange: true)
            .Build();

        var container = new ContainerBuilder()
            .InstallGlobalCancellationToken(configuration)
            .InstallConfig(configuration)
            .InstallSerilog(configuration)
            .InstallDatabase(configuration)
            .InstallSingletons(configuration)
            .InstallScoped(configuration)
            .Build();

        using (var scope = container.BeginLifetimeScope())
        {
            Log.Information("Loading assembly commands");
            var commandRepo = scope.Resolve<CommandRepository>();
            commandRepo.LoadAssemblyCommands();

            // The simplest way of handling the bot's channel/user is just initializing it here.
            var config = scope.Resolve<IConfiguration>();
            var tlp = scope.Resolve<IThreeLetterAPI>();
            var db = scope.Resolve<DatabaseContext>();
            var ctoken = scope.Resolve<CancellationTokenSource>().Token;

            var botChannel = await db.Channels
                .Where(x => x.UserTwitchID.Equals(config["Twitch:UserID"]))
                .SingleOrDefaultAsync();

            if (botChannel is null)
            {
                var response = await tlp.SendAsync<BasicUserResponse>(new BasicUserInstruction(), new { id = config["Twitch:UserID"] }, ctoken);

                UserDTO user = new()
                {
                    TwitchID = response.User.ID,
                    TwitchName = response.User.Login,
                };

                ChannelDTO channel = new()
                {
                    TwitchID = response.User.ID,
                    TwitchName = response.User.Login,
                    UserTwitchID = response.User.ID,
                };

                // add to database
                db.Channels.Add(channel);
                db.Users.Add(user);

                await db.SaveChangesAsync();
            }


            Log.Information("Checking for Pending migrations");
            await db.Database.MigrateAsync(ctoken);

            await scope.Resolve<Application>().StartAsync();
        }




        Console.ReadLine();

        //var builder = new ContainerBuilder();
        //builder.RegisterType<Idiot>().InstancePerDependency();
        //builder.RegisterType<Dependency>().InstancePerDependency();
        //var container = builder.Build();

        //////using (var scope = container.BeginLifetimeScope(b => b.RegisterType<Idiot>().SingleInstance()))
        //////{
        //////    var idiot = scope.Resolve<Idiot>();
        //////    idiot.Foo();
        //////}

        ////SomeData Data = new("Foo");

        ////// register Data in the lifetime scope and resolve in Idiot
        ////using (var scope = container.BeginLifetimeScope(b => b.RegisterInstance(Data)))
        ////{
        ////    var idiot = scope.Resolve<Idiot>();
        ////    idiot.Foo();
        ////}

        //Activator.CreateInstance(

        //    );

        //Console.ReadLine();
    }
}

//public interface IIdiot
//{
//    void Foo();
//}

//public record SomeData(string Foo);

//public class Idiot : IIdiot
//{
//    public required Dependency Dependency { protected get; init; }

//    public SomeData Data { get; }

//    public Idiot(SomeData data)
//    {
//        Data = data;
//    }

//    public void Foo()
//    {
//        Console.WriteLine(this.Data.Foo);

//        this.Dependency.Yes();
//    }
//}

//public class Dependency
//{
//    public void Yes()
//    {
//        Console.WriteLine("Yes");
//    }
//}