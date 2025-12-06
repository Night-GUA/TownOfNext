using AmongUs.GameOptions;
using TONX.Modules;
using Hazel;

namespace TONX.Roles.Crewmate;
public sealed class Recruit : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Recruit),
            player => new Recruit(player),
            CustomRoles.Recruit,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            23200,
            SetupOptionItem,
            "rr",
            "#17FFA3"
        );
    public Recruit(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }

    static OptionItem OptionSkillCooldown;
    static OptionItem OptionSkillDuration;
    static OptionItem OptionSkillNums;
    enum OptionName
    {
        RecruitSkillCooldown,
        RecruitSkillDuration,
        RecruitSkillMaxOfUseage,
    }

    private int SkillLimit;
    private long ProtectStartTime;
    private static void SetupOptionItem()
    {
        OptionSkillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.RecruitSkillCooldown, new(2.5f, 180f, 2.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionSkillDuration = FloatOptionItem.Create(RoleInfo, 11, OptionName.RecruitSkillDuration, new(2.5f, 180f, 2.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionSkillNums = IntegerOptionItem.Create(RoleInfo, 12, OptionName.RecruitSkillMaxOfUseage, new(1, 99, 1), 5, false)
            .SetValueFormat(OptionFormat.Times);
    }
    public override void Add()
    {
        SkillLimit = OptionSkillNums.GetInt();
        ProtectStartTime = 0;
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown =
            ProtectStartTime != 0 ? OptionSkillDuration.GetFloat() + 1 :
            (SkillLimit <= 0 ? 255f : OptionSkillCooldown.GetFloat());
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }
    public override bool GetAbilityButtonText(out string text)
    {
        text = GetString("RecruitButtonText");
        return true;
    }
    public override bool GetAbilityButtonSprite(out string buttonName)
    {
        buttonName = "Recruit";
        return true;
    }
    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (ProtectStartTime != 0) return false;
        if (SkillLimit >= 1)
        {
            SkillLimit--;
            SendRpc();
            ProtectStartTime = Utils.GetTimeStamp();
            if (!Player.IsModClient()) Player.RpcProtectedMurderPlayer(Player);
            Player.RPCPlayCustomSound("Gunload");
            Player.Notify(GetString("RecruitOnPull"), OptionSkillDuration.GetFloat());
            Player.MarkDirtySettings();
        }
        else
        {
            Player.Notify(GetString("SkillMaxUsage"));
        }
        return false;
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (ProtectStartTime == 0) return;
        if (ProtectStartTime + (long)OptionSkillDuration.GetFloat() < Utils.GetTimeStamp())
        {
            ProtectStartTime = 0;
            player.RpcProtectedMurderPlayer();
            player.SyncSettings();
            player.RpcResetAbilityCooldown();
            player.Notify(string.Format(GetString("RecruitOffPull"), SkillLimit));
        }
    }
    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        if (info.IsSuicide) return true;
        if (ProtectStartTime != 0 && ProtectStartTime + (long)OptionSkillDuration.GetFloat() >= Utils.GetTimeStamp())
        {
            var (killer, target) = info.AttemptTuple;
            target.RpcMurderPlayerV2(killer);
            // MyState.DeathReason = CustomDeathReason.Sacrifice;
            PlayerState.GetByPlayerId(killer.PlayerId).DeathReason = CustomDeathReason.Backlash;
            Logger.Info($"新兵 {target.GetRealName()} 扔出手榴弹，与凶手 {killer.GetRealName()} 同归于尽", "Recruit.OnCheckMurderAsTarget");
        }
        return true;
    }
    public override int OverrideAbilityButtonUsesRemaining() => SkillLimit;
    private void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(SkillLimit);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        SkillLimit = reader.ReadInt32();
    }
}