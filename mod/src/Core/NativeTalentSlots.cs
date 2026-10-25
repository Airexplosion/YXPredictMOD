namespace YxPrediction
{
    // Current native collapsed talent slots, in the game's original realm/slot order.
    public static class NativeTalentSlots
    {
        public const int Count = 6;
        public const float CenterX = 270f, CenterY = 182f, ContainerSize = 528f, IconSize = 128f;
        public static float X(int slot)
        {
            switch(slot){case 0:return 0f;case 1:return -105f;case 2:return -190f;case 3:return -197f;case 4:return -148f;case 5:return -46f;default:return 0f;}
        }
        public static float Y(int slot)
        {
            switch(slot){case 0:return -145.9f;case 1:return -128.4f;case 2:return -60.9f;case 3:return 40.6f;case 4:return 137.4f;case 5:return 189f;default:return 0f;}
        }
        public static float Scale(int slot)
        {
            switch(slot){case 0:return 0.6f;case 1:return 0.65f;case 2:return 0.7f;case 3:return 0.75f;case 4:return 0.8f;case 5:return 0.8f;default:return 1f;}
        }
    }
}
