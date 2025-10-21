// 커서 상태만 관리하는 초소형 컴포넌트
using UnityEngine;

public class SwitchCursorState : MonoBehaviour //CursorState가 필요함? 왜?

{
	int _idx = -1;

	public int GetAndAdvance(int count, int startIndex, bool advanceNow)
	{
		if (count <= 0) return 0;
		if (_idx < 0) _idx = Mathf.Clamp(startIndex, 0, count - 1);
		int cur = _idx;
		if (advanceNow) _idx = (_idx + 1) % count;
		return cur;
	}

	// OnHit에서 수동 증가가 필요할 때 호출
	public void AdvanceExplicit(int count) => _idx = (_idx + 1 + count) % count;
}