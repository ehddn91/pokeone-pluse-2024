﻿using Poke1Bot.Modules;
using Poke1Bot.Scripting;
using Poke1Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Poke1Bot
{
    public class BotClient
    {
        public enum State
        {
            Stopped,
            Started,
            Paused
        };

        public AccountManager AccountManager { get; }
        public GameClient Game { get; private set; }
        public BattleAI AI { get; private set; }
        public BaseScript Script { get; private set; }
        public Random Rand { get; } = new Random();
        public Account Account { get; set; }
        public State Running { get; private set; }
        public MovementResynchronizer MovementResynchronizer { get; }
        public AutoReconnector AutoReconnector { get; }
        public PokemonEvolver PokemonEvolver { get; }
        public MoveTeacher MoveTeacher { get; }
        public AutoLootBoxOpener AutoLootBoxOpener { get; }
        public QuestManager QuestManager { get; }
        public UserSettings Settings { get; }

        private Npc _npcBattler;


        public event Action<State> StateChanged;
        public event Action<string> MessageLogged;
        public event Action<string, Brush> ColorMessageLogged;
        public event Action ClientChanged;
        public event Action ConnectionOpened;
        public event Action ConnectionClosed;


        private ProtocolTimeout _actionTimeout = new ProtocolTimeout();

        private bool _loginRequested;

        public BotClient()
        {
            AccountManager = new AccountManager("Accounts");
            PokemonEvolver = new PokemonEvolver(this);
            MovementResynchronizer = new MovementResynchronizer(this);
            MoveTeacher = new MoveTeacher(this);
            AutoReconnector = new AutoReconnector(this);
            AutoLootBoxOpener = new AutoLootBoxOpener(this);
            QuestManager = new QuestManager(this);
            Settings = new UserSettings();

#if DEBUG
            byte[] bytes = new byte[] { 0x50, 0x6a, 0x03, 0xc2, 0xcf, 0x68, 0x90, 0xe6, 0xba, 0x32, 0x3c, 0x1a, 0x08, 0x00, 0x45, 0x00, 0x00, 0x28, 0x78, 0x91, 0x40, 0x00, 0x80, 0x06, 0x00, 0x00, 0xc0, 0xa8, 0x01, 0x0e, 0x5f, 0xb7, 0x30, 0x44, 0xed, 0x6f, 0x07, 0xdc, 0x54, 0x7f, 0x6b, 0x80, 0x7d, 0x82, 0x1a, 0x41, 0x50, 0x10, 0x01, 0x00, 0x51, 0xcc, 0x00, 0x00 };
            var pc = Convert.ToBase64String(bytes);

            Console.WriteLine(pc);

            var enc = Poke1Protocol.StringCipher.EncryptOrDecryptToBase64Byte("username;)", "db2a1b6e-34d9-46ae-b319-d58bfc71011d");

            var s64 = new PSXAPI.Request.Ack
            {
                Data = enc
            };

            Console.WriteLine(Encoding.UTF8.GetString(s64.Data));

            //var packet = @"InventoryPokemon CtEBChIJ8TZvO060XkYRhk8er86fOWQSjgEIowEQCxjPCyAkKgYIXRAZGBkqBggtECgYKCoGCCEQIxgjKgYIXxAUGBQwAjhoQAtSDAgTEAQYHyAJKAEwDloAYAJqEgm/i6xDrmcMTxGCrF/kjgIhyXISCb+LrEOuZwxPEYKsX+SOAiHJgAEEkAHDruOwAaIBBhACIAEwBqoBCwi+8s/AlfvlNhAFsAEDGgwIJBAMGBAgCygRMBEiBXhjb2RlKgV4Y29kZTIIS2VlbiBFeWU4swpAwA0Q////////////ARj///////////8BIP///////////wE=";
            //var packet = @"Transfer CAESEgnxNm87TrReRhGGTx6vzp85ZA==";
            //var packet = @"Reorder ChIJ8TZvO060XkYRhk8er86fOWQKEgkmzfVu9CvLSRGQRb3nB/7fDAoSCfYOcUXnX0tPEaMc+TIXvZ2yChIJYRN0DkiWxU0RgB2UOdBYu10KEgnc3Xc6Zt6VQRG0ts8aa5F9XQ==";
            var packet = @"Battle CgZ8c3BsaXQKCXxjaG9pY2V8fAokfGNob2ljZXxtb3ZlIHNjcmF0Y2ggMiwgbW92ZSB3cmFwIDJ8CiZ8Y2hvaWNlfHxtb3ZlIHNsdWRnZSAxLCBtb3ZlIGFpcmN1dHRlcgpBfGNob2ljZXxtb3ZlIHNjcmF0Y2ggMiwgbW92ZSB3cmFwIDJ8bW92ZSBzbHVkZ2UgMSwgbW92ZSBhaXJjdXR0ZXIKAXwKPHxtb3ZlfHAyYjogWnViYXR8QWlyIEN1dHRlcnxwMWI6IEJlbGxzcHJvdXR8W3NwcmVhZF0gcDFhLHAxYgoSfC1jcml0fHAxYTogRnVycmV0CgZ8c3BsaXQKGXwtZGFtYWdlfHAxYTogRnVycmV0fDkvNDgKGnwtZGFtYWdlfHAxYTogRnVycmV0fDExLzU1Chl8LWRhbWFnZXxwMWE6IEZ1cnJldHw5LzQ4Chp8LWRhbWFnZXxwMWE6IEZ1cnJldHwxMS81NQogfC1zdXBlcmVmZmVjdGl2ZXxwMWI6IEJlbGxzcHJvdXQKBnxzcGxpdAoefC1kYW1hZ2V8cDFiOiBCZWxsc3Byb3V0fDAgZm50Ch58LWRhbWFnZXxwMWI6IEJlbGxzcHJvdXR8MCBmbnQKHnwtZGFtYWdlfHAxYjogQmVsbHNwcm91dHwwIGZudAoefC1kYW1hZ2V8cDFiOiBCZWxsc3Byb3V0fDAgZm50ChZ8ZmFpbnR8cDFiOiBCZWxsc3Byb3V0CiR8bW92ZXxwMWE6IEZ1cnJldHxTY3JhdGNofHAyYjogWnViYXQKEXwtY3JpdHxwMmI6IFp1YmF0CgZ8c3BsaXQKGHwtZGFtYWdlfHAyYjogWnViYXR8OS80OAoYfC1kYW1hZ2V8cDJiOiBadWJhdHw5LzQ4Chl8LWRhbWFnZXxwMmI6IFp1YmF0fDExLzU1Chl8LWRhbWFnZXxwMmI6IFp1YmF0fDExLzU1CiV8bW92ZXxwMmE6IEtvZmZpbmd8U2x1ZGdlfHAxYTogRnVycmV0ChJ8LWNyaXR8cDFhOiBGdXJyZXQKBnxzcGxpdAoafC1kYW1hZ2V8cDFhOiBGdXJyZXR8MCBmbnQKGnwtZGFtYWdlfHAxYTogRnVycmV0fDAgZm50Chp8LWRhbWFnZXxwMWE6IEZ1cnJldHwwIGZudAoafC1kYW1hZ2V8cDFhOiBGdXJyZXR8MCBmbnQKEnxmYWludHxwMWE6IEZ1cnJldAoBfAoHfHVwa2VlcBLyCgoCcDEQAxrpCggDEAEoASgBOt4KCgJwMRICcDEakgIKCnAxOiBGdXJyZXQSDkZ1cnJldCwgTDE2LCBGGgUwIGZudCABKgoIIRAbGBggGygdMgdhZ2lsaXR5MgdzY3JhdGNoMgtkZWZlbnNlY3VybDILcXVpY2thdHRhY2s6B3J1bmF3YXlCAEoHcG9rYmFsbFILZ2lidXloaWx0b25YisziyQFiHAoHQWdpbGl0eRIHYWdpbGl0eRgeIB4qBHNlbGZiHgoHU2NyYXRjaBIHc2NyYXRjaBghICMqBm5vcm1hbGIlCgxEZWZlbnNlIEN1cmwSC2RlZmVuc2VjdXJsGCggKCoEc2VsZmInCgxRdWljayBBdHRhY2sSC3F1aWNrYXR0YWNrGB4gHioGbm9ybWFsGpcCCg5wMTogQmVsbHNwcm91dBISQmVsbHNwcm91dCwgTDE2LCBNGgUwIGZudCABKgoIIBASGBwgDygSMgxwb2lzb25wb3dkZXIyBmdyb3d0aDIEd3JhcDILc2xlZXBwb3dkZXI6C2NobG9yb3BoeWxsQgBKB3Bva2JhbGxSC2dpYnV5aGlsdG9uWJe0h5YHYikKDVBvaXNvbiBQb3dkZXISDHBvaXNvbnBvd2RlchgjICMqBm5vcm1hbGIaCgZHcm93dGgSBmdyb3d0aBgUIBQqBHNlbGZiGAoEV3JhcBIEd3JhcBgTIBQqBm5vcm1hbGInCgxTbGVlcCBQb3dkZXISC3NsZWVwcG93ZGVyGA8gDyoGbm9ybWFsGvABCgpwMTogTWVvd3RoEg5NZW93dGgsIEwxNywgRhoFNDIvNDIqCggWEBIYEiAVKCQyB3NjcmVlY2gyB3NjcmF0Y2gyBGJpdGUyB2Zha2VvdXQ6BnBpY2t1cEIASgdwb2tiYWxsUgtnaWJ1eWhpbHRvbljJwvVkYh4KB1NjcmVlY2gSB3NjcmVlY2gYKCAoKgZub3JtYWxiHgoHU2NyYXRjaBIHc2NyYXRjaBgjICMqBm5vcm1hbGIYCgRCaXRlEgRiaXRlGBYgGSoGbm9ybWFsYh8KCEZha2UgT3V0EgdmYWtlb3V0GAogCioGbm9ybWFsGpYCCgtwMTogR2VvZHVkZRIPR2VvZHVkZSwgTDI0LCBNGgU2MC82MCoKCCgQOhgTIBQoFzIMc2VsZmRlc3RydWN0MgZ0YWNrbGUyCG11ZHNwb3J0Mgpyb2NrcG9saXNoOgZzdHVyZHlCAEoHcG9rYmFsbFILZ2lidXloaWx0b25Y6LC1lwNiLgoNU2VsZi1EZXN0cnVjdBIMc2VsZmRlc3RydWN0GAUgBSoLYWxsQWRqYWNlbnRiHAoGVGFja2xlEgZ0YWNrbGUYIyAjKgZub3JtYWxiHgoJTXVkIFNwb3J0EghtdWRzcG9ydBgPIA8qA2FsbGIjCgtSb2NrIFBvbGlzaBIKcm9ja3BvbGlzaBgUIBQqBHNlbGYamAIKDHAxOiBWZW51c2F1chIQVmVudXNhdXIsIEwzMywgTRoFMCBmbnQqCgg/EEMYSCBYKDsyA2N1dDILc2xlZXBwb3dkZXIyCnBldGFsZGFuY2UyCXdvcnJ5c2VlZDoIb3Zlcmdyb3dCAEoIcG9rZWJhbGxSC2dpYnV5aGlsdG9uWJLJw44HYhYKA0N1dBIDY3V0GB4gHioGbm9ybWFsYicKDFNsZWVwIFBvd2RlchILc2xlZXBwb3dkZXIYDyAPKgZub3JtYWxiKwoLUGV0YWwgRGFuY2USCnBldGFsZGFuY2UYBSAKKgxyYW5kb21Ob3JtYWxiIwoKV29ycnkgU2VlZBIJd29ycnlzZWVkGAogCioGbm9ybWFsGucMCgJwMhADGt4MIAE62QwKAnAyEgJwMhqRAgoLcDI6IEtvZmZpbmcSD0tvZmZpbmcsIEwyMywgRhoFNTEvNTEgASoKCCIQKxggIBsoFTIJYXNzdXJhbmNlMgljbGVhcnNtb2cyBnNsdWRnZTIMc2VsZmRlc3RydWN0OghsZXZpdGF0ZUIASghwb2tlYmFsbFiZtul3YiIKCUFzc3VyYW5jZRIJYXNzdXJhbmNlGAogCioGbm9ybWFsYiMKCkNsZWFyIFNtb2cSCWNsZWFyc21vZxgPIA8qBm5vcm1hbGIcCgZTbHVkZ2USBnNsdWRnZRgSIBQqBm5vcm1hbGIuCg1TZWxmLURlc3RydWN0EgxzZWxmZGVzdHJ1Y3QYBSAFKgthbGxBZGphY2VudBqYAgoJcDI6IFp1YmF0Eg1adWJhdCwgTDI1LCBNGgUxMS81NSABKgoIGxAYGBIgGSggMgpjb25mdXNlcmF5MglhaXJjdXR0ZXIyBXN3aWZ0Mgpwb2lzb25mYW5nOgppbm5lcmZvY3VzQgBKCHBva2ViYWxsWJTJmrQGYiUKC0NvbmZ1c2UgUmF5Egpjb25mdXNlcmF5GAogCioGbm9ybWFsYiwKCkFpciBDdXR0ZXISCWFpcmN1dHRlchgYIBkqD2FsbEFkamFjZW50Rm9lc2IjCgVTd2lmdBIFc3dpZnQYEyAUKg9hbGxBZGphY2VudEZvZXNiJQoLUG9pc29uIEZhbmcSCnBvaXNvbmZhbmcYDyAPKgZub3JtYWwa9wEKC3AyOiBSYXR0YXRhEg9SYXR0YXRhLCBMMjMsIEYaBTQ2LzQ2KgoIHhAVGBAgFSgmMgdwdXJzdWl0MgloeXBlcmZhbmcyCWFzc3VyYW5jZTIGY3J1bmNoOgRndXRzQgBKCHBva2ViYWxsWMrazNAFYh4KB1B1cnN1aXQSB3B1cnN1aXQYFCAUKgZub3JtYWxiIwoKSHlwZXIgRmFuZxIJaHlwZXJmYW5nGA8gDyoGbm9ybWFsYiIKCUFzc3VyYW5jZRIJYXNzdXJhbmNlGAogCioGbm9ybWFsYhwKBkNydW5jaBIGY3J1bmNoGA8gDyoGbm9ybWFsGpYCCglwMjogWnViYXQSDVp1YmF0LCBMMjUsIEYaBTU1LzU1KgoIGxAWGBQgGSggMgpjb25mdXNlcmF5MglhaXJjdXR0ZXIyBXN3aWZ0Mgpwb2lzb25mYW5nOgppbm5lcmZvY3VzQgBKCHBva2ViYWxsWLWl5uUHYiUKC0NvbmZ1c2UgUmF5Egpjb25mdXNlcmF5GAogCioGbm9ybWFsYiwKCkFpciBDdXR0ZXISCWFpcmN1dHRlchgZIBkqD2FsbEFkamFjZW50Rm9lc2IjCgVTd2lmdBIFc3dpZnQYFCAUKg9hbGxBZGphY2VudEZvZXNiJQoLUG9pc29uIEZhbmcSCnBvaXNvbmZhbmcYDyAPKgZub3JtYWwa9gEKDHAyOiBSYXRpY2F0ZRIQUmF0aWNhdGUsIEwyMywgRhoFNTgvNTgqCggqECAYGSAlKDUyBGJpdGUyB3B1cnN1aXQyCWh5cGVyZmFuZzIJYXNzdXJhbmNlOgdydW5hd2F5QgBKCHBva2ViYWxsWMXk9b0GYhgKBEJpdGUSBGJpdGUYGSAZKgZub3JtYWxiHgoHUHVyc3VpdBIHcHVyc3VpdBgUIBQqBm5vcm1hbGIjCgpIeXBlciBGYW5nEgloeXBlcmZhbmcYDyAPKgZub3JtYWxiIgoJQXNzdXJhbmNlEglhc3N1cmFuY2UYCiAKKgZub3JtYWwakwIKCXAyOiBadWJhdBINWnViYXQsIEwyMywgRhoFNTEvNTEqCggZEBIYEiAZKB4yCndpbmdhdHRhY2syCmNvbmZ1c2VyYXkyCWFpcmN1dHRlcjIFc3dpZnQ6CmlubmVyZm9jdXNCAEoIcG9rZWJhbGxY78OTwwRiIgoLV2luZyBBdHRhY2sSCndpbmdhdHRhY2sYIyAjKgNhbnliJQoLQ29uZnVzZSBSYXkSCmNvbmZ1c2VyYXkYCiAKKgZub3JtYWxiLAoKQWlyIEN1dHRlchIJYWlyY3V0dGVyGBkgGSoPYWxsQWRqYWNlbnRGb2VzYiMKBVN3aWZ0EgVzd2lmdBgUIBQqD2FsbEFkamFjZW50Rm9lcyoLZ2lidXloaWx0b24qC2dpYnV5aGlsdG9uKgtnaWJ1eWhpbHRvbioLZ2lidXloaWx0b24qC2dpYnV5aGlsdG9uQANYAWAW";


            var data = packet.Split(" ".ToCharArray());

            byte[] array = Convert.FromBase64String(data[1]);
            var type = Type.GetType($"PSXAPI.Request.{data[0]}, PSXAPI");
            goto RESP;
            if (type != null)
            {
                var proto = typeof(PSXAPI.Proto).GetMethod("Deserialize").MakeGenericMethod(new Type[]
                {
                    type
                }).Invoke(null, new object[]
                {
                    array
                }) as PSXAPI.IProto;

                //var s = proto as PSXAPI.Request.Ack;

                //string decodedString = Encoding.UTF8.GetString(s.Data);
                //Console.WriteLine(decodedString);
                //if (proto is null)
                //    goto RESP;

                Console.WriteLine(ToJsonString(proto));
                return;
                //Console.WriteLine($"MapLoad: {(proto as PSXAPI.Request.BattleBroadcast).RequestID}, ID: {(proto as PSXAPI.Request.BattleBroadcast)._Name.ToString()}");
            }
            RESP:
            type = Type.GetType($"PSXAPI.Response.{data[0]}, PSXAPI");
            if (type != null)
            {
                var proto = typeof(PSXAPI.Proto).GetMethod("Deserialize").MakeGenericMethod(new Type[]
                {
                        type
                }).Invoke(null, new object[]
                {
                        array
                }) as PSXAPI.IProto;
                Console.WriteLine(ToJsonString(proto));
                // Console.WriteLine($"MapLoad: {(proto as PSXAPI.Request.BattleBroadcast).RequestID}, ID: {(proto as PSXAPI.Request.BattleBroadcast)._Name.ToString()}");
            }
#endif
        }
        private static string ToJsonString(PSXAPI.IProto p) => Newtonsoft.Json.JsonConvert.SerializeObject(p, new Newtonsoft.Json.JsonSerializerSettings
        {
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
            Formatting = Newtonsoft.Json.Formatting.Indented
        });

        public void LogMessage(string message)
        {
            MessageLogged?.Invoke(message);
        }

        public void LogMessage(string message, Brush color)
        {
            ColorMessageLogged?.Invoke(message, color);
        }

        public void Login(Account account)
        {
            Account = account;
            _loginRequested = true;
        }
        private void LoginUpdate()
        {
            GameClient client;
            if (Account.Socks.Version != SocksVersion.None)
            {
                // TODO: Clean this code.
                client = new GameClient(new GameConnection((int)Account.Socks.Version, Account.Socks.Host, Account.Socks.Port, Account.Socks.Username, Account.Socks.Password),
                    new MapConnection((int)Account.Socks.Version, Account.Socks.Host, Account.Socks.Port, Account.Socks.Username, Account.Socks.Password));
            }
            else
            {
                client = new GameClient(new GameConnection(), new MapConnection());
            }
            SetClient(client);
            client.Open();
        }

        public void Logout(bool allowAutoReconnect)
        {
            if (!allowAutoReconnect)
            {
                AutoReconnector.IsEnabled = false;
            }
            Game.Close();
        }

        public void Update()
        {

            if (_loginRequested)
            {
                LoginUpdate();
                _loginRequested = false;
                return;
            }

            AutoReconnector.Update();
            AutoLootBoxOpener.Update();
            QuestManager.Update();

            if (_npcBattler != null && Game != null && Game.IsMapLoaded && Game.IsInactive)
            {
                Game.ClearPath();
                TalkToNpc(_npcBattler);
                return;
            }

            if (Script?.IsLoaded == true)
                Script?.Update();

            if (Running != State.Started)
            {
                return;
            }

            if (PokemonEvolver.Update()) return;
            if (MoveTeacher.Update()) return;
            if (AI != null && AI.IsBusy) return;

            if (Game.IsMapLoaded && Game.AreNpcReceived && Game.IsInactive)
            {
                ExecuteNextAction();
            }
        }

        public void Start()
        {
            if (Game != null && Script != null && Running == State.Stopped)
            {
                _actionTimeout.Set();
                Running = State.Started;
                StateChanged?.Invoke(Running);
                Script.Start();
            }
        }

        public void Pause()
        {
            if (Game != null && Script != null && Running != State.Stopped)
            {
                if (Running == State.Started)
                {
                    Running = State.Paused;
                    StateChanged?.Invoke(Running);
                    Script.Pause();
                }
                else
                {
                    Running = State.Started;
                    StateChanged?.Invoke(Running);
                    Script.Resume();
                }
            }
        }

        public void Stop()
        {
            if (Game != null)
                Game.ClearPath();

            if (Running != State.Stopped)
            {
                Running = State.Stopped;
                StateChanged?.Invoke(Running);
                if (Script != null)
                {
                    Script.Stop();
                }
            }
        }

        public async Task LoadScript(string filename)
        {
            using (var reader = new StreamReader(filename))
            {
                var input = await reader.ReadToEndAsync();

                List<string> libs = new List<string>();
                if (Directory.Exists("Libs"))
                {
                    string[] files = Directory.GetFiles("Libs");
                    foreach (string file in files)
                    {
                        if (file.ToUpperInvariant().EndsWith(".LUA"))
                        {
                            using (var streaReader = new StreamReader(file))
                            {
                                libs.Add(await streaReader.ReadToEndAsync());
                            }
                        }
                    }
                }

                BaseScript script = new LuaScript(this, Path.GetFullPath(filename), input, libs);

                Stop();
                Script = script;
            }

            try
            {
                Script.ScriptMessage += Script_ScriptMessage;
                await Script.Initialize();
            }
            catch (Exception)
            {
                Script = null;
                throw;
            }
        }

        public bool OpenPC()
        {
            var pcNpcs = Game.Map.Npcs.FindAll(npc => Game.Map.IsPc(npc.PositionX, npc.PositionY)).OrderBy(npc => Game.DistanceTo(npc.PositionX, npc.PositionY)).ToList();
            if (pcNpcs is null || pcNpcs.Count <= 0)
                return false;
            var pcNpc = pcNpcs.FirstOrDefault();
            if (pcNpc is null)
                return false;

            return TalkToNpc(pcNpc);
        }

        public bool TalkToNpc(Npc target)
        {
            bool canInteract = Game.Map.CanInteract(Game.PlayerX, Game.PlayerY, target.PositionX, target.PositionY);
            if (canInteract)
            {
                //var fromNpcDir = target.GetDriectionFrom(Game.PlayerX, Game.PlayerY);
                //if (fromNpcDir == Game._lastDirection)
                //{

                //}
                //else if (!target.IsInLineOfSight(Game.PlayerX, Game.PlayerY))
                //{
                //    var oneStep = new[] { fromNpcDir.ToOneStepMoveActions() };
                //    Game.SendMovement(oneStep, Game.PlayerX, Game.PlayerY);
                //    Game._lastDirection = fromNpcDir;
                //}
                _npcBattler = null;
                Game.TalkToNpc(target);
                return true;
            }
            return MoveToCell(target.PositionX, target.PositionY, 1);
        }

        public bool MoveToCell(int x, int y, int requiredDistance = 0)
        {
            MovementResynchronizer.CheckMovement(x, y);

            Pathfinding path = new Pathfinding(Game);
            bool result;

            if (Game.PlayerX == x && Game.PlayerY == y)
            {
                result = path.MoveToSameCell();
            }
            else
            {
                result = path.MoveTo(x, y, requiredDistance);
            }

            if (result)
            {
                MovementResynchronizer.ApplyMovement(x, y);
            }
            return result;
        }

        public bool MoveToNearestLink()
        {
            var links = Game.Map.Links.OrderBy(link => GameClient.DistanceBetween(Game.PlayerX, Game.PlayerY, link.x, -link.z));
            foreach (var link in links)
                if (MoveToCell(link.x, -link.z))
                    return true;
            return false;
        }

        public bool CanMoveTo(int x, int y)
        {
            Pathfinding path = new Pathfinding(Game);
            return path.CanMoveTo(x, y);
        }

        public bool MoveToAreaLink(string destinationMap)
        {
            IEnumerable<Tuple<int, int>> nearest = Game.Map.GetNearestLinks(destinationMap, Game.PlayerX, Game.PlayerY);
            if (nearest != null)
            {
                foreach (Tuple<int, int> link in nearest)
                {
                    if (MoveToCell(link.Item1, link.Item2))
                        return true;
                }
            }
            return false;
        }

        private bool IsInArea(MAPAPI.Response.Area area, int x, int y)
        {
            if (x >= area.StartX && x <= area.EndX && y >= area.StartY && y <= area.EndY)
            {
                return true;
            }
            return false;
        }

        public bool MoveLeftRight(int startX, int startY, int destX, int destY)
        {
            bool result;
            if (startX != destX && startY != destY)
            {
                // ReSharper disable once RedundantAssignment
                return false;
            }
            if (Game.PlayerX == destX && Game.PlayerY == destY)
            {
                result = MoveToCell(startX, startY);
            }
            else if (Game.PlayerX == startX && Game.PlayerY == startY)
            {
                result = MoveToCell(destX, destY);
            }
            else
            {
                result = MoveToCell(startX, startY);
            }
            return result;
        }

        public bool IsAreaLink(int x, int y)
        {
            var pathFinder = new Pathfinding(Game);
            return Game != null && Game.IsMapLoaded && Game.Map.IsAreaLink(x, y) && pathFinder.CanMoveTo(x, y);
        }

        public void SetClient(GameClient client)
        {
            Game = client;
            AI = null;
            Stop();

            if (client != null)
            {
                AI = new BattleAI(client);
                client.ConnectionOpened += Client_ConnectionOpened;
                client.ConnectionFailed += Client_ConnectionFailed;
                client.ConnectionClosed += Client_ConnectionClosed;
                client.BattleMessage += Client_BattleMessage;
                client.SystemMessage += Client_SystemMessage;
                client.ServerCommandException += Client_ServerCommandException;
                client.DialogOpened += Client_DialogOpened;
                client.TeleportationOccuring += Client_TeleportationOccuring;
                client.LogMessage += LogMessage;
                client.MoveToBattleWithNpc += Client_MoveToBattleWithNpc;
            }
            ClientChanged?.Invoke();
        }

        private void Client_MoveToBattleWithNpc(Npc battler)
        {
            _npcBattler = battler;
        }

        private void Client_ServerCommandException(string msg)
        {
            Stop();
            LogMessage("Server occurred an exception: " + msg, Brushes.Firebrick);
        }

        private void ExecuteNextAction()
        {
            try
            {
                bool executed = Script.ExecuteNextAction();
                if (!executed && Running != State.Stopped && !_actionTimeout.Update())
                {
                    LogMessage("No action executed: stopping the bot.");
                    Stop();
                }
                else if (executed)
                {
                    _actionTimeout.Set();
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                LogMessage("Error during the script execution: " + ex);
#else
                LogMessage("Error during the script execution: " + ex.Message);
#endif
                Stop();
            }
        }

        private void Client_ConnectionOpened()
        {
            ConnectionOpened?.Invoke();
            Game.SendAuthentication(Account.Name, Account.Password);
        }
        private void Client_ConnectionClosed(Exception ex)
        {
            if (ex != null)
            {
#if DEBUG
                LogMessage("Disconnected from the server: " + ex, Brushes.OrangeRed);
#else
                LogMessage("Disconnected from the server: " + ex.Message, Brushes.OrangeRed);
#endif
            }
            else
            {
                LogMessage("Disconnected from the server.", Brushes.OrangeRed);
            }
            ConnectionClosed?.Invoke();
            SetClient(null);
        }
        private void Client_ConnectionFailed(Exception ex)
        {
            if (ex != null)
            {
#if DEBUG
                LogMessage("Could not connect to the server: " + ex, Brushes.OrangeRed);
#else
                LogMessage("Could not connect to the server: " + ex.Message, Brushes.OrangeRed);
#endif
            }
            else
            {
                LogMessage("Could not connect to the server.", Brushes.OrangeRed);
            }
            ConnectionClosed?.Invoke();
            SetClient(null);
        }
        private void Client_SystemMessage(string message)
        {
            if (Script != null)
                Script.OnSystemMessage(message);
        }
        private void Client_BattleMessage(string message)
        {
            if (Script != null)
                Script.OnBattleMessage(message);
        }

        private void Client_DialogOpened(string message)
        {
            if (Script != null)
                Script.OnDialogMessage(message);
        }

        private void Client_TeleportationOccuring(string map, int x, int y)
        {
            string message = "Position updated: [" + map + "] (" + x + ", " + y + ")";
            if (Game.Map == null || Game.IsTeleporting)
            {
                message += " [OK]";
            }
            else if (Game.MapName != map)
            {
                message += " [WARNING, different map] /!\\";
                Script?.OnWarningMessage(true);
            }
            else
            {
                int distance = GameClient.DistanceBetween(x, y, Game.PlayerX, Game.PlayerY);
                if (distance < 8)
                {
                    message += " [OK, lag, distance=" + distance + "]";
                }
                else
                {
                    message += " [WARNING, distance=" + distance + "] /!\\";
                    Script?.OnWarningMessage(false, distance);
                }
                Script?.OnMovementLag(distance);
            }

            if (message.Contains("OK"))
            {
                LogMessage(message, Brushes.LimeGreen);
            }
            if (message.Contains("WARNING"))
            {
                LogMessage(message, Brushes.OrangeRed);
            }

            MovementResynchronizer.Reset();
        }

        private void Script_ScriptMessage(string message)
        {
            LogMessage(message);
        }
    }
}
