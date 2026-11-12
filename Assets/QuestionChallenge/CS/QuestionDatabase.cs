
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "QuestionDatabase",
    menuName = "Question Challenge/Question Database"
)]
public class QuestionDatabase : ScriptableObject
{
    [Header("Question Settings")]
    public List<QuestionData> questions = new List<QuestionData>();

    public List<QuestionData> GetQuestionsByCategory(
        QuestionCategory category)
    {
        return questions.FindAll(q => q.category == category);
    }

    public List<QuestionData> GetAllQuestions()
    {
        return new List<QuestionData>(questions);
    }
}
