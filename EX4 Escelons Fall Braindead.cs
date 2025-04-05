using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SplatoonScriptsOfficial.Duties.Dawntrail;
public unsafe class EX4_Escelons_Fall : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories { get; } = [1271];

    public override Metadata? Metadata => new(6, "NightmareXIV, Redmoonwow Modified for braindead");


        uint StatusCloseFar = 2970;
        uint StatusParamClose = 758;
        uint StatusParamFar = 759;
        uint[] CastSwitcher = { 43182, 43181 };
        uint RoseBloom3rd = 43541;
        uint NpcNameId = 13861;
        int NumSwitches = 0;
        long ForceResetAt = long.MaxValue;
        List<bool> SequenceIsClose = new List<bool>();
        bool AdjustPhase = false;
        bool THShockTargeted = false;

        IBattleNpc? Zelenia => Svc.Objects.OfType<IBattleNpc>().FirstOrDefault(x => x.NameId == this.NpcNameId && x.IsTargetable);

        public override void OnSetup()
        {
            Controller.RegisterElementFromCode("Out", "{\"Name\":\"Out\",\"type\":1,\"Enabled\":false,\"radius\":6.0,\"fillIntensity\":0.25,\"originFillColor\":1677721855,\"endFillColor\":1677721855,\"refActorNPCNameID\":13861,\"refActorComparisonType\":6,\"onlyTargetable\":true,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0,\"refActorTetherConnectedWithPlayer\":[]}");
            Controller.RegisterElementFromCode("In", "{\"Name\":\"In\",\"type\":1,\"Enabled\":false,\"radius\":6.0,\"Donut\":20.0,\"fillIntensity\":0.25,\"originFillColor\":1677721855,\"endFillColor\":1677721855,\"refActorNPCNameID\":13861,\"refActorComparisonType\":6,\"onlyTargetable\":true,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0,\"refActorTetherConnectedWithPlayer\":[]}");
            Controller.RegisterElementFromCode("InIncorrect", "{\"Name\":\"\",\"type\":1,\"radius\":1.0,\"Filled\":false,\"fillIntensity\":0.5,\"overlayTextColor\":4278190335,\"thicc\":5.0,\"overlayText\":\">>> IN <<<\",\"refActorType\":1,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
            Controller.RegisterElementFromCode("InCorrect", "{\"Name\":\"\",\"type\":1,\"radius\":1.0,\"color\":3355508480,\"Filled\":false,\"fillIntensity\":0.5,\"overlayTextColor\":3355508480,\"thicc\":5.0,\"overlayText\":\"> IN <\",\"refActorType\":1,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
            Controller.RegisterElementFromCode("OutIncorrect", "{\"Name\":\"\",\"type\":1,\"radius\":1.0,\"color\":3372155135,\"Filled\":false,\"fillIntensity\":0.5,\"overlayTextColor\":3372155135,\"thicc\":5.0,\"overlayText\":\"<<< OUT >>>\",\"refActorType\":1,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
            Controller.RegisterElementFromCode("OutCorrect", "{\"Name\":\"\",\"type\":1,\"radius\":1.0,\"color\":3355508480,\"Filled\":false,\"fillIntensity\":0.5,\"overlayTextColor\":3355508480,\"thicc\":5.0,\"overlayText\":\"< OUT >\",\"refActorType\":1,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
        }

        public override void OnSettingsDraw()
        {
            ImGuiEx.Text("Role Selection");
            bool dpsChanged = ImGui.Checkbox("I am DPS", ref C.IsDPS);
            ImGui.SameLine();
            bool supportChanged = ImGui.Checkbox("I am Support", ref C.IsSupport);

            if (dpsChanged)
            {
                if (C.IsDPS)
                {
                    C.IsSupport = false;
                }
                else if (!C.IsSupport)
                {
                    C.IsDPS = true;
                }
            }
            if (supportChanged)
            {
                if (C.IsSupport)
                {
                    C.IsDPS = false;
                }
                else if (!C.IsDPS)
                {
                    C.IsSupport = true;
                }
            }

            ImGuiEx.HelpMarker("DPS always start IN. Support always start OUT.");
            ImGui.Separator();
            ImGui.SetNextItemWidth(150f);
            ImGuiEx.SliderInt("Delay, ms", ref C.Delay, 0, 1000);
            ImGuiEx.HelpMarker("Delay helps to synchronize script with attack animation. Set to 0 for instant feedback.");
            if (ImGui.CollapsingHeader("Debug"))
            {
                ImGui.Checkbox("AdjustPhase", ref AdjustPhase);
                ImGui.Checkbox("THShockTargeted", ref THShockTargeted);
                ImGuiEx.Text($"SequenceIsClose: {SequenceIsClose.Print()}");
                ImGuiEx.Text($"GetMyCloses: {GetMyCloses().Print()}");
                ImGuiEx.Text($"IsSelfClose: {IsSelfClose()}");
                ImGuiEx.Text($"NumSwitches: {NumSwitches}");
                ImGuiEx.Text($"ForceResetAt: {ForceResetAt}");
                ImGuiEx.Text($"{Svc.Objects.OfType<IPlayerCharacter>().OrderBy(x => Vector3.Distance(x.Position, Zelenia.Position)).Print("\n")}");
            }
        }

        public override void OnGainBuffEffect(uint sourceId, Status status)
        {
            if (sourceId == Zelenia?.EntityId && status.StatusId == this.StatusCloseFar)
            {
                SequenceIsClose.Add(this.StatusParamClose == status.Param);
                PluginLog.Debug($"Registered: {(SequenceIsClose.Last() ? "Close" : "Far")}");
            }
        }

        float GetThickness(bool isMyClose)
        {
            var isBaiting = this.SequenceIsClose[this.NumSwitches] == isMyClose;
            if (isBaiting)
            {
                var factor = (Environment.TickCount64 / 30) % 20;
                if (factor > 10) factor = 20 - factor;
                return factor;
            }
            return 5;
        }

        float GetRadius(bool isIn)
        {
            var z = Zelenia;
            if (z == null) return 5f;
            var breakpoint = Svc.Objects.OfType<IPlayerCharacter>().OrderBy(x => Vector2.Distance(x.Position.ToVector2(), z.Position.ToVector2())).ToList().SafeSelect(isIn ? 4 : 3);
            if (breakpoint == null) return 5f;
            var distance = Vector2.Distance(z.Position.ToVector2(), breakpoint.Position.ToVector2());
            return Math.Max(0.5f, distance);
        }

        List<bool> GetMyCloses()
        {
            bool startClose = C.IsDPS;
            if (this.AdjustPhase)
            {
                if (THShockTargeted)
                {
                    startClose = !C.IsDPS;
                }
                else
                {
                    startClose = C.IsDPS;
                }
            }
            return new List<bool> { startClose, !startClose, startClose, !startClose };
        }

        bool IsSelfClose()
        {
            if (Zelenia == null) return false;
            return Svc.Objects.OfType<IPlayerCharacter>().OrderBy(x => Vector2.Distance(x.Position.ToVector2(), Zelenia.Position.ToVector2())).Take(4).Any(x => x.AddressEquals(Player.Object));
        }

        public override void OnUpdate()
        {
            Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
            if (Environment.TickCount64 > ForceResetAt || NumSwitches >= 4)
            {
                ForceResetAt = long.MaxValue;
                Reset();
                return;
            }

            if (this.SequenceIsClose.Count >= 1)
            {
                var isMyClose = GetMyCloses()[this.NumSwitches];
                var correct = IsSelfClose() == isMyClose;
                var e = Controller.GetElementByName(isMyClose ? "In" : "Out");
                e.Enabled = true;
                e.radius = GetRadius(isMyClose);
                var e2 = Controller.GetElementByName(isMyClose ? $"In{(correct ? "Correct" : "Incorrect")}" : $"Out{(correct ? "Correct" : "Incorrect")}");
                e2.Enabled = true;
                e2.thicc = GetThickness(isMyClose);
            }
        }

        public override void OnActionEffectEvent(ActionEffectSet set)
        {
            if (set.Action == null) return;
            if (set.Action.Value.RowId.EqualsAny(this.CastSwitcher))
            {
                PluginLog.Information("Switch");
                if (C.Delay > 0)
                {
                    this.Controller.Schedule(() => NumSwitches++, C.Delay);
                }
                else
                {
                    NumSwitches++;
                }
            }
        }

        public override void OnDirectorUpdate(DirectorUpdateCategory category)
        {
            if (category.EqualsAny(DirectorUpdateCategory.Complete, DirectorUpdateCategory.Recommence, DirectorUpdateCategory.Wipe)) Reset();
        }

        public override void OnStartingCast(uint source, uint castId)
        {
            if (castId == this.RoseBloom3rd)
            {
                PluginLog.Information("Next Escelons Need Adjust");
                AdjustPhase = true;
            }
        }

        public override void OnVFXSpawn(uint target, string vfxPath)
        {
            if (AdjustPhase && vfxPath.Contains("vfx/lockon/eff/x6fd_shock_lock2v.avfx"))
            {
                if (target.TryGetObject(out var obj) && obj is IPlayerCharacter pc && !THShockTargeted)
                {
                    if (pc.GetJob() is Job.DRK or Job.WAR or Job.GNB or Job.PLD or Job.WHM or Job.AST or Job.SCH or Job.SGE)
                    {
                        PluginLog.Information($"TH Shock Targeted: {pc.Name.ToString()}");
                        THShockTargeted = true;
                    }
                }
            }
        }

        void Reset()
        {
            this.SequenceIsClose.Clear();
            NumSwitches = 0;
            Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
            AdjustPhase = false;
            THShockTargeted = false;
        }

        Config C => Controller.GetConfig<Config>();
        public class Config : IEzConfig
        {
            public bool IsDPS = true;
            public bool IsSupport = false;
            public int Delay = 800;
        }
    }
}
