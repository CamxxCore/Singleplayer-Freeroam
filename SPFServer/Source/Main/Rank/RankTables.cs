namespace SPFServer.Ranks
{
    public static class RankTables
    {
        public static int GetRankIndex(int value)
        {
            for (int i = 0; i < RankData.Length; i += 2)
            {
                if (value >= RankData[i] && value < RankData[i + 1])
                    return i;
            }
            return -1;
        }

        public static readonly int[] RankData = new int[]
        {
            0, 89,
            90, 199,
            200, 349, 
            350, 499,
            500, 739,
            740, 999,
            1000, 1599,
            1600, 1999,
            2000, 2699,
            2700, 3499,
            3500, 4199,
            4200, 5399,
            5400, 6599,
            6600, 7999,
            8000, 9299, 
            9300, 10899,
            10900, 12199,
            12200, 13499,
            13500, 13899,
            13900, 14299,
            14300, 14799,
            14800, 15299,
            15300, 15799,
            15800, 16399,
            16400, 16999,
            17000, 18399,
            18400, 18999,
            19000, 24299,
            24300, 24999,
            25000, 29999,
            30000, 100000,
            100000, 100000
        };
    }
}
