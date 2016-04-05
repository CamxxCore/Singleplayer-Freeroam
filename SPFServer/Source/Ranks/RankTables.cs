﻿namespace SPFServer.Ranks
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
            0, 19,
            19, 49,
            50, 89, //0
            90, 199, //1
            200, 349, //2
            350, 499, //3
            500, 739, //4
            740, 999, //5
            1000, 1599, //6
            1600, 1999, //7
            2000, 2699, //8
            2700, 3499, //9
            3500, 4199, //10
            4200, 5399, //11
            5400, 6599, //12
            6600, 7999, //13
            8000, 9299,  //14
            9300, 10899, //15
            10900, 12199, //16
            12200, 13499, //17
            13500, 13899, //18
            13900, 14299, //19
            14300, 14799, //20
            14800, 15299, //21
            15300, 15799, //22
            15800, 16399, //23
            16400, 16999, //24
            17000, 18399, //25
            18400, 18999,  //26
            19000, 24299, //27
            24300, 24999, //28
            25000, 29999, //29
            30000, 100000,
            100000, 100000
        };
    }
}
