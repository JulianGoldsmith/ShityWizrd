using System.Net;
using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(LineRenderer))]
public class ConnectionControllerUI : MonoBehaviour
{
    private VisualEffect _visualEffect;

    private Transform _startPoint;
    private Transform _endPoint;
    public SocketUI startSocket { get; private set; }
    public SocketUI endSocket { get; private set; }

    private void Awake()
    {
        _visualEffect = GetComponent<VisualEffect>();
    }

    public void Initialize(Transform start, Transform end, Color color)
    {
        _startPoint = start;
        _endPoint = end;
        if(start != null)
        {
            this.startSocket = start.GetComponent<SocketUI>();
            //this.startSocket.transform.position = start.position;
        }
        if (end != null)
        {
            this.endSocket = end.GetComponent<SocketUI>();
            //this.endSocket.transform.position = end.position;
        }

        _visualEffect.SetVector4("Color", color);
    }


    private void Update()
    {
        if (_startPoint != null && _endPoint != null)
        {
            transform.position = (_startPoint.position + _endPoint.position) / 2;
            transform.rotation = Quaternion.LookRotation(_endPoint.position - _startPoint.position);
            transform.localScale = new Vector3(transform.localScale.x, transform.localScale.y, (_endPoint.position - _startPoint.position).magnitude);
            _visualEffect.SetVector3("StartPoint", _startPoint.position);
            _visualEffect.SetVector3("EndPoint", _endPoint.position);
        }
    }
}