
using System;
using UnityEngine;

public enum QuestionCategory
{
    GeneralKnowledge,
    Science,
    Technology,
    History,
    Geography,
    Sports,
    English,
    Riddles
}

[Serializable]
public class QuestionData
{
    [TextArea(2, 5)]
    public string questionText;

    public QuestionCategory category;

    [Header("Four Answer Options")]
    public string[] answers = new string[4];

    [Header("Correct Answer")]
    [Range(0, 3)]
    public int correctAnswerIndex;
}
