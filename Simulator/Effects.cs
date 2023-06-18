namespace Craftimizer.Simulator;

public record struct Effects
{
    public byte InnerQuiet { get; set; }
    public byte WasteNot { get; set; }
    public byte Veneration { get; set; }
    public byte GreatStrides { get; set; }
    public byte Innovation { get; set; }
    public byte FinalAppraisal { get; set; }
    public byte WasteNot2 { get; set; }
    public byte MuscleMemory { get; set; }
    public byte Manipulation { get; set; }
    public bool HeartAndSoul { get; set; }

    public void SetDuration(EffectType effect, byte duration)
    {
        switch (effect)
        {
            case EffectType.InnerQuiet:
                if (duration == 0)
                    InnerQuiet = 0;
                break;
            case EffectType.WasteNot:
                WasteNot = duration;
                break;
            case EffectType.Veneration:
                Veneration = duration;
                break;
            case EffectType.GreatStrides:
                GreatStrides = duration;
                break;
            case EffectType.Innovation:
                Innovation = duration;
                break;
            case EffectType.FinalAppraisal:
                FinalAppraisal = duration;
                break;
            case EffectType.WasteNot2:
                WasteNot2 = duration;
                break;
            case EffectType.MuscleMemory:
                MuscleMemory = duration;
                break;
            case EffectType.Manipulation:
                Manipulation = duration;
                break;
            case EffectType.HeartAndSoul:
                HeartAndSoul = duration != 0;
                break;
        }
    }

    public void Strengthen(EffectType effect)
    {
        if (effect == EffectType.InnerQuiet && InnerQuiet < 10)
            InnerQuiet++;
    }

    public byte GetDuration(EffectType effect) =>
        effect switch
        {
            EffectType.InnerQuiet => (byte)(InnerQuiet != 0 ? 1 : 0),
            EffectType.WasteNot => WasteNot,
            EffectType.Veneration => Veneration,
            EffectType.GreatStrides => GreatStrides,
            EffectType.Innovation => Innovation,
            EffectType.FinalAppraisal => FinalAppraisal,
            EffectType.WasteNot2 => WasteNot2,
            EffectType.MuscleMemory => MuscleMemory,
            EffectType.Manipulation => Manipulation,
            EffectType.HeartAndSoul => (byte)(HeartAndSoul ? 1 : 0),
            _ => 0
        };

    public byte GetStrength(EffectType effect) =>
        effect == EffectType.InnerQuiet ? InnerQuiet :
        (byte)(GetDuration(effect) != 0 ? 1 : 0);

    public bool HasEffect(EffectType effect) =>
        GetDuration(effect) != 0;

    public void DecrementDuration()
    {
        if (WasteNot > 0)
            WasteNot--;
        if (WasteNot2 > 0)
            WasteNot2--;
        if (Veneration > 0)
            Veneration--;
        if (GreatStrides > 0)
            GreatStrides--;
        if (Innovation > 0)
            Innovation--;
        if (FinalAppraisal > 0)
            FinalAppraisal--;
        if (MuscleMemory > 0)
            MuscleMemory--;
        if (Manipulation > 0)
            Manipulation--;
    }
}
