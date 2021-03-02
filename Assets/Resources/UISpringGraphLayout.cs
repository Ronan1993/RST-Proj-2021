using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.Events;
using System;
using Newtonsoft.Json;

public class UISpringGraphLayout : MonoBehaviour {

    public List<Item> itemCollection;
    private List<Item> orderedItemCollection = new List<Item>();

    public float springLength = 30.0f;
	public float springk = 12.2f;
	public float repulsion = 0.005f;
	public float damping = 0.005f;
    public float centerk = 0.0001f;

    public float minEnergyThreshold = 5.0f;

    [Space(10)]

    public bool isFullyConnected = false;
    public bool autoCreateNodes = false;

	LayoutElement layoutElement;

	public class Edge {
		public Node n1;
		public Node n2;
        public float weight;
		public Edge(Node _n1, Node _n2){
			n1 = _n1;
			n2 = _n2;
		}

		public bool isActive {
			get { return n1.isActive && n2.isActive; }
		}
	}

	public class Node {
		public RectTransform transform;
		public Vector2 velocity;
		public Vector2 acceleration;

        public HashSet<Node> adjacentNodes = new HashSet<Node>();

		public Node(RectTransform t){
			transform = t;
			velocity = new Vector2();
			acceleration = new Vector2();
		}

		public void AddForce(Vector2 f){
			acceleration += f;			
		}

		public Vector2 position {
			get { return transform.anchoredPosition; }
			set { transform.anchoredPosition = value; }
		}

		public Rect rect {
			get { return transform.rect; }
		}

		public bool isActive {
			get { return transform.gameObject.activeSelf; }
		}

	}

    public UnityEvent OnAtRest = new UnityEvent();

	List<Node> nodes = new List<Node>();
	List<Edge> edges = new List<Edge>();

    bool isAtRest = false;

	void Awake()
    {
		layoutElement = GetComponent<LayoutElement> ();
	}

	// Use this for initialization
	void Start () {

        int index = 0;

        try
        {
            var JsonFile = Resources.Load<TextAsset>("Items");
            itemCollection = JsonConvert.DeserializeObject<List<Item>>(JsonFile.text);
        }
        catch (Exception ex)
        {
            itemCollection = new List<Item>();
        }

        foreach (Item item in itemCollection) {

            GameObject itemObj = new GameObject("Item " + item.itemID);
            itemObj.AddComponent<RectTransform>();
            itemObj.GetComponent<RectTransform>().SetParent(transform);
            itemObj.transform.Translate(new Vector3(index, index, 0));

            GameObject imageObj = new GameObject("Image for Slide " + item.itemID);
            Image image = imageObj.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>(item.photo);
            imageObj.GetComponent<RectTransform>().SetParent(itemObj.transform);
            imageObj.SetActive(true);

            // Make spacing smaller now

            GameObject textObj = new GameObject("Text for Slide " + item.itemID);
            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.color = Color.red;
            text.resizeTextForBestFit = true;
            text.text = item.text;
            textObj.GetComponent<RectTransform>().SetParent(itemObj.transform);
            textObj.transform.Translate(new Vector3(0, -1 * imageObj.GetComponent<RectTransform>().rect.height, 0));
            textObj.SetActive(true);

            index++;
        }

        /*if (autoCreateNodes)
        {*/
		foreach (RectTransform t in transform)
        {
            AddNode(t);
		}
        //}

        AddRelations();



        /*(if (isFullyConnected)
        {
			for (int i = 0; i < nodes.Count; ++i){
				for (int j = i; j < nodes.Count; ++j){
					if (i == j)
						continue;
					AddEdge(nodes[i], nodes[j]);
				}
			}         
        }*/
    }

    public void AddRelations()
    {
        int index = 0;
        
        foreach(Node node in nodes)
        {
            foreach(Relation relation in itemCollection[index].relations)
            { 
                AddEdge(node, GetRelationNode(relation));  
            }
            index++;
        }
    }

    // Next thing is to alter the string length based on relation and put the nodes closer to the center, or in a random location, using an RNG

    Node GetRelationNode(Relation relation)
    {
        return nodes[relation.otherItem - 1];
    }

    public Node AddNode(RectTransform t)
    {
        Node n = new Node(t);
        nodes.Add(n);
        return n;
	}

    public Edge AddEdge(Node n1, Node n2)
    {
        if (n1 == null || n2 == null)
            return null;

        if (edges.Any(x => (x.n1 == n1 && x.n2 == n2) || (x.n1 == n2 && x.n2 == n1)))
        {
            return edges.Where(x => (x.n1 == n1 && x.n2 == n2) || (x.n1 == n2 && x.n2 == n1)).FirstOrDefault();
        }

        Edge e = new Edge(n1, n2);
        edges.Add(e);
        n1.adjacentNodes.Add(n2);
        n2.adjacentNodes.Add(n1);
        return e;
    }

    public Edge AddEdge(RectTransform r1, RectTransform r2)
    {        
		Node n1 = nodes.Find(x => x.transform == r1);
		Node n2 = nodes.Find(x => x.transform == r2);

        return AddEdge(n1, n2);
    }
	
	// Update is called once per frame
	void Update () 
    {
		if (nodes.Count == 0 && autoCreateNodes)
        {
			foreach (RectTransform t in transform)
            {
			    AddNode(t);
		    }
            if (isFullyConnected)
            {
				for (int i = 0; i < nodes.Count; ++i){
					for (int j = i; j < nodes.Count; ++j){
						if (i == j)
							continue;
						edges.Add(new Edge(nodes[i], nodes[j]));
					}
				}
			}             
        }

		ApplyHookesLaw();
		ApplyColombsLaw();
		AttractToCenter ();
		UpdateVelocity();
		UpdatePos();

        if (TotalEnergy() < minEnergyThreshold)
        {
            if (!isAtRest)
            {
				isAtRest = true;
				OnAtRest.Invoke();                
            }
        } else 
        {
            isAtRest = false;
        }
	}

	void ApplyHookesLaw()
    {
		foreach (var e in edges)
        {
            if (!e.isActive)
            {
                continue;
            }
            Vector2 d = e.n2.position  - e.n1.position;
			float displacement = springLength - d.magnitude;
            Vector2 direction = d.normalized;

			e.n1.AddForce(springk * direction * displacement * -0.5f);
			e.n2.AddForce(springk * direction * displacement * 0.5f);
		}
	}

	void ApplyColombsLaw()
    {
		foreach (var n1 in nodes){
			foreach (var n2 in nodes){

                if (!n1.isActive || !n2.isActive)
                {
                    continue;
                }

				if (n1 == n2)
					continue;

                if (n1.adjacentNodes.Contains(n2))
                    continue;

                Vector2 d = n1.position - n2.position;

				float distance = d.magnitude + 0.001f;
                Vector2 direction = d.normalized;

                Vector2 force = ((repulsion) / (distance * distance)) * direction;
				n1.AddForce(force);
				n2.AddForce(-force);
			}			
		}
	}

	void AttractToCenter()
    {
		foreach (Node n in nodes) {
            if (!n.isActive)
            {
                continue;
            }
			var p = -n.position;
			n.AddForce (p * centerk);
		}
	}

	void UpdateVelocity()
    {
		for (int i = 0; i < nodes.Count; ++i){			
			Node n = nodes[i];
			n.velocity = (n.velocity + n.acceleration) * damping;
            n.acceleration = new Vector2();
		}
	}

	void UpdatePos()
    {
		Rect r = new Rect ();
		foreach (Node n in nodes){
            n.position += n.velocity * Time.deltaTime;

			if (n.isActive) {
				
				r = Rect.MinMaxRect (
					Mathf.Min (n.rect.xMin + n.transform.anchoredPosition.x, r.xMin), 
					Mathf.Min (n.rect.yMin + n.transform.anchoredPosition.y, r.yMin), 
					Mathf.Max (n.rect.xMax + n.transform.anchoredPosition.x, r.xMax), 
					Mathf.Max (n.rect.xMax + n.transform.anchoredPosition.y, r.yMax));
			}
		}

        if (layoutElement != null)
        {
			layoutElement.minWidth = r.width;
			layoutElement.preferredWidth = r.width;
			layoutElement.minHeight = r.height;            
        }
	}

    float TotalEnergy()
    {
        float energy = 0.0f;
        foreach (Node n in nodes)
        {
            energy += 0.5f * Mathf.Pow(n.velocity.magnitude, 2);
        }
        return energy;
    }
}
