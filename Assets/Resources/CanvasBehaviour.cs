using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Newtonsoft.Json;
using UnityEngine.UI;

public class CanvasBehaviour : MonoBehaviour
{
    public List<Item> itemCollection;
    private IEnumerator coroutine;
    private List<Item> orderedItemCollection = new List<Item>();
    private float delay = 2.0f;

    // Start is called before the first frame update
    void Start()
    {
        try
        {
            var JsonFile = Resources.Load<TextAsset>("Items");
            itemCollection = JsonConvert.DeserializeObject<List<Item>>(JsonFile.text);
            OrderItems();
            coroutine = PresentItems();
            StartCoroutine(coroutine);
        }
        catch (Exception ex)
        {
            itemCollection = new List<Item>();
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OrderItems()
    {
        SelectFirstAction();
    }

    void SelectFirstAction()
    {
        foreach (Item item in itemCollection)
        {
            AllocateInitialRating(item);
        }

        List<Item> possibleFirstItems = GetPossibleStartingItems(GetMaxScore());

        TraverseItems(TieBreak(possibleFirstItems)); 
    }

    void AddItem(Item item)
    {
        orderedItemCollection.Add(item);
    }

    IEnumerator PresentItems()
    {
        foreach (Item item in orderedItemCollection)
        {
            Debug.Log("Item ID: " + item.itemID + " - " + item.text);
            var text = gameObject.GetComponentInChildren<Text>();
            var image = gameObject.GetComponentInChildren<Image>();
            text.text = item.text;
            image.sprite = Resources.Load<Sprite>(item.photo);
            yield return new WaitForSeconds(delay);
        }
    }

    void TraverseItems(Item item)
    {
        AddItem(item);
        item.completed = true;

        while (HasLinksToIncompleteItems(item)) // Problem is item is the third, once the third is over, it needs to refer to the 2nd
        {
            AllocateTransitionRatings(item);
            TraverseItems(GetNextItem(item)); // Need item to be item2 when item3 is finished (when it returns to item2's iteration)
        }
    }

    bool HasLinksToIncompleteItems(Item item)
    {
        foreach (Relation relation in item.relations)
        {
            if (!GetRelationItem(relation).completed)
            {
                return true;
            }
        }
        return false;
    }

    Item GetNextItem(Item item)
    {
        // Get the relation(s) in the highest priority bracket
        List<Relation> possibleTransitions = GetPossibleTransitions(item);

        // Tie breaker, if there are multiple relations in the same priority bracket
        if (possibleTransitions.Count > 1)
        {
            return TieBreak(possibleTransitions);
        }
        // If there's only one item in highest priority bracket, transition to it
        else if (possibleTransitions.Count == 1)
        {
            return GetRelationItem(possibleTransitions[0]);
        }
        else
        {
            return null;
        }
    }

    void AllocateInitialRating(Item item)
    {
        foreach (Relation relation in item.relations)
        {
            int ranking = GetInitialRanking(relation);

            if (ranking > item.score)
            {
                item.score = ranking;
            }
        }
    }

    int GetInitialRanking(Relation relation)
    {
        switch (GetRelationCode(relation))
        {
            case "prep-sat":
                return 2;
            case "back-nuc":
                return 1;            
            case "join-ele":
                return 0;
            case "cont-ele":
                return 0;
            case "sequ-ele":
                return 0;            
            default:
                return -1;
        }
    }


    void AllocateTransitionRatings(Item item)
    {
        // Set rankings for each possible path of current item
        foreach (Relation relation in item.relations)
        {
            SetTransitionRelationRankings(relation);
        }
    }

    void SetTransitionRelationRankings(Relation relation)
    {
        if (GetRelationItem(relation).completed)
        {
            relation.score = -1;
        }
        else
        {
            switch (GetRelationCode(relation))
            {
                case "join-ele":
                    relation.score = 8;
                    break;
                case "prep-sat":
                    relation.score = 7;
                    break;
                case "cont-ele":
                    relation.score = 6;
                    break;
                case "sequ-ele":
                    relation.score = 5;
                    break;
                case "back-sat":
                    relation.score = 4;
                    break;
                case "back-nuc":
                    relation.score = 3;
                    break;
                case "evid-nuc":
                    relation.score = 2;
                    break;
                case "eval-nuc":
                    relation.score = 1;
                    break;
                default:
                    relation.score = 0;
                    break;
            }
        }
    }

    List<Relation> GetPossibleTransitions(Item item)
    {
        List<Relation> possibleTrans = new List<Relation>();
        int maxScore = -1;

        // Determine which transition makes the most logical sense
        foreach (Relation relation in item.relations)
        {
            if (relation.score > maxScore)
            {
                possibleTrans = new List<Relation>();
                possibleTrans.Add(relation);
                maxScore = relation.score;
            }
            else if ((relation.score != -1) && (relation.score == maxScore))
            {
                possibleTrans.Add(relation);
                maxScore = relation.score;
            }
        }
        return possibleTrans;
    }

    int GetMaxScore()
    {
        int maxScore = -1;

        if (itemCollection.Count > 0)
        {
            maxScore = itemCollection[0].score;
        }

        foreach (Item item in itemCollection)
        {
            if (item.score > maxScore)
            {
                maxScore = item.score;
            }
        }
        return maxScore;
    }

    List<Item> GetPossibleStartingItems(int maxScore)
    {
        List<Item> possibleItems = new List<Item>();

        foreach (Item item in itemCollection)
        {
            if(item.score >= maxScore)
            {
                possibleItems.Add(item);
            }
        }
        return possibleItems;
    }

    Item TieBreak(List<Relation> possibleTrans)
    {
        Item nextItem = GetRelationItem(possibleTrans[0]);
        int maxNumberOfRelations = GetRelationItem(possibleTrans[0]).relations.Count;

        foreach (Relation relation in possibleTrans)
        {
            if (GetRelationItem(relation).relations.Count > maxNumberOfRelations)
            {
                nextItem = GetRelationItem(relation);
                maxNumberOfRelations = GetRelationItem(relation).relations.Count;
            }
        }
        return nextItem;
    }


    Item TieBreak(List<Item> possibleItems)
    {
        Item nextItem = possibleItems[0];
        int maxNumberOfRelations = possibleItems[0].relations.Count;

        foreach (Item item in possibleItems)
        {
            if(item.relations.Count > maxNumberOfRelations)
            {
                nextItem = item;
                maxNumberOfRelations = item.relations.Count;
            }
        }
        return nextItem;
    }

    Item GetRelationItem(Relation relation)
    {
        return itemCollection[relation.otherItem - 1];
    }

    string GetRelationCode(Relation relation)
    {
        return relation.relationName.Substring(0, 4) + "-" + relation.role.Substring(0, 3);
    }
}

[System.Serializable]
public class Item
{
    public int itemID;
    public string text;
    public string photo;
    public List<Relation> relations;
    public bool completed = false;
    public int score = -1; 
}

[System.Serializable]
public class Relation
{
    public string relationName;
    public string role;
    public int otherItem;
    public int score = -1;
}