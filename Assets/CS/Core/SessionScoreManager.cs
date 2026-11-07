using UnityEngine;

public static class SessionScoreManager
{
    // حفظ انتصارات الجلسة الحالية فقط
    public static int HostWins = 0;
    public static int ClientWins = 0;

    /// <summary>
    /// إضافة فوز للمستضيف
    /// </summary>
    public static void AddHostWin()
    {
        HostWins++;
    }

    /// <summary>
    /// إضافة فوز للضيف
    /// </summary>
    public static void AddClientWin()
    {
        ClientWins++;
    }

    /// <summary>
    /// تصفير النتائج بالكامل فور مغادرة الغرفة أو إغلاقها
    /// </summary>
    public static void ResetSession()
    {
        HostWins = 0;
        ClientWins = 0;
        Debug.Log("[SessionScoreManager] تم تصفير نتائج الجلسة بالكامل.");
    }
}