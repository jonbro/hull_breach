using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Unit {
	public Vector2 position;
	public float fullHealth = 100;
	float _health = 100;
	public float health {
		get{
			return _health;
		}
		set{
			_health = (int)Mathf.Min(value, fullHealth);
		}
	}
	public int owner;
	public float displaySize;
	public bool alive = true;
	public int xpValue = 1;
	float _attackPower = 20;
	public float attackPower{
		get{
			return _attackPower;
		}
		set{
			_attackPower = value;
		}
	}
	float _attackCooldown = 1.0f;
	public float attackCooldown{
		get {
			return _attackCooldown;
		}
		set {
			_attackCooldown = value;
		}
	}
	public Vector3 pos3{
		get{
			return new Vector3(position.x, position.y, 25);
		}
	}
	public void SetHealth(float thishealth){
		fullHealth = _health = thishealth;
	}

}

public class UnitBase : MonoBehaviour {
	public Unit u = new Unit();
	public float attackRadius;
	protected int pid;
	public Color ourColor;
	public LineRenderManager lines;
	List<UnitBase> targets;
	public bool attacking;
	public float attackCooldownCounter;
	float attackCoolRatio;
	public PlayerManager player;
	public float temporaryDistance;
	void Awake(){
		targets = new List<UnitBase>();
		transform.parent = GameObject.Find("HBGameController").transform;
	}
	void Start(){
		attackCooldownCounter =  u.attackCooldown;
		lines = GameObject.Find("LineRenderManager").GetComponent<LineRenderManager>();
	}
	public void Setup(int _pid, Color _ourColor){
		pid = _pid;
		u.owner = pid;
		ourColor = _ourColor;
	}
	void Update(){
		attackCooldownCounter -= Time.deltaTime;
		Draw();
	}
	void Draw(){
		attackCoolRatio = Mathf.Min(1, Mathf.Max(0, 1-attackCooldownCounter/u.attackCooldown));
		Vector2 bp = u.position;
		lines.AddCircle(new Vector3(bp.x, bp.y, 25), u.displaySize*attackCoolRatio, ourColor, Time.time * 20, 3);
		lines.AddCircle(new Vector3(bp.x, bp.y, 25), u.displaySize, ourColor, 10);
		int numPoints = 25;
		int currentHealth = (int)Mathf.Floor((u.health/u.fullHealth)*25.0f);
		for(int i=0;i<currentHealth;i++){
			float sAngle = (i/(float)(numPoints)*360.0f-Time.time*40.0f)*Mathf.Deg2Rad;
			Vector3 pa = new Vector3(Mathf.Cos(sAngle)*u.displaySize+u.position.x, Mathf.Sin(sAngle)*u.displaySize+u.position.y, 25);
			float ar = attackRadius+((Mathf.Sin(Time.time*4.0f+i)+1.0f)*0.5f)*(attackRadius-u.displaySize);
			Vector3 pb = new Vector3(Mathf.Cos(sAngle)*ar+u.position.x, Mathf.Sin(sAngle)*ar+u.position.y, 25);
			lines.AddLine(pa, pb, new Color(ourColor.r, ourColor.g, ourColor.b, 0.25f));
		}
	}
	[RPC]
	public void SetHealth(float newHealth){
		u.health = newHealth;
	}

	[RPC]
	public void SetCooldown(float newCooldown){
		attackCooldownCounter = newCooldown;
	}

	[RPC]
	public void Explode(){
		AudioManager.Instance.Deaths(u.owner+1);
		lines.AddCircleExplosion(new Vector3(u.position.x, u.position.y, 25), u.displaySize*attackCoolRatio, ourColor, Time.time * 20, 3);
		lines.AddCircleExplosion(new Vector3(u.position.x, u.position.y, 25), u.displaySize, ourColor, 10);		
	}
	public void DisplayWinner(){
		transform.position = u.pos3+Vector3.left;
		transform.localScale = new Vector3(10, 10, 10);
		lines.GetComponent<DrawString>().Text(((int)u.health).ToString(), transform, 0.2f, ourColor);
	}
	public void CheckNeighbors(UnitBase[] units){
		if(attackCooldownCounter>0){
			return;
		}
		if(GetComponent<PlayerManager>()){
			GetComponent<PlayerManager>().CheckNeighbors(units);
			return;
		}
		if(GetComponent<CoreC>()){
			GetComponent<CoreC>().CheckNeighbors(units);
			return;
		}
		targets.Clear();
		foreach(UnitBase unit in units){
			float dist = Vector2.Distance(unit.u.position, u.position);
			if(dist < attackRadius && unit.u.owner != u.owner && unit.u.alive){
				unit.temporaryDistance = dist;
				targets.Add(unit);
			}
		}
		targets.Sort(delegate(UnitBase p1, UnitBase p2)
		    {
		        return (p1.temporaryDistance < p2.temporaryDistance)?-1:1;
		    }
		);
		attacking = true;
		if(targets.Count > 0){
			AttackTarget(targets[0]);
		}else{
			attacking = false;
		}
	}
	void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
	{
	    if (stream.isWriting)
	    {
	        // We own this player: send the others our data
	        stream.SendNext(u.position);
	        stream.SendNext(attacking);
	    }
	    else
	    {
	        // Network player, receive data
	        this.u.position = (Vector2)stream.ReceiveNext();
	        this.attacking = (bool)stream.ReceiveNext();
	    }
	}

	public void AttackTarget(UnitBase target){
		// draw a line to the unit we would be attacking
		if(target == null || (target.GetComponent<PlayerManager>() && target.GetComponent<PlayerManager>().respawning)){
			return;
		}
		lines.AddLine(u.pos3.Variation(2), target.u.pos3.Variation(2), ourColor);
		attackCooldownCounter = u.attackCooldown;
		GetComponent<PhotonView>().RPC("SetCooldown", PhotonTargets.AllBuffered, u.attackCooldown);
		target.GetComponent<PhotonView>().RPC("SetHealth", PhotonTargets.AllBuffered, target.u.health - u.attackPower);
		target.GetComponent<PhotonView>().RPC("GotHit", PhotonTargets.All);
		if(target.u.health <= 0){
			target.u.alive = false;
		}
	}

	[RPC]
	void GotHit(){
		lines.exploding = true;
		Draw();
		lines.exploding = false;
	}
}