using System;

namespace Megabonk.Multiplayer
{
    [Flags]
    internal enum PlayerInputButtons : ushort
    {
        None        = 0,
        Jump        = 1 << 0,
        Attack      = 1 << 1,
        AbilityOne  = 1 << 2,
        AbilityTwo  = 1 << 3,
        Dash        = 1 << 4,
        Interact    = 1 << 5,
        Sprint      = 1 << 6
    }
}
