namespace CrazyArcade.GameCore
{
    /// <summary>
    /// 맵 타일의 종류를 정의하는 enum
    /// </summary>
    public enum TileType : byte
    {
        /// <summary>
        /// 일반 땅 - 모든 플레이어가 이동 가능
        /// </summary>
        Ground = 0,

        /// <summary>
        /// 물 타일 - 상어만 이동 가능, 연쇄 폭발 트리거
        /// 폭발 시 파괴되지 않고 연쇄 폭발만 발생
        /// </summary>
        Water = 1,

        /// <summary>
        /// 벽 타일 - 파괴 불가능한 고정 장애물
        /// </summary>
        Wall = 2,

        /// <summary>
        /// 산호초 타일 1 - 파괴 가능, 아이템 드롭 가능
        /// </summary>
        Coral1 = 3,

        /// <summary>
        /// 산호초 타일 2 - 파괴 가능, 아이템 드롭 가능
        /// </summary>
        Coral2 = 4,

        /// <summary>
        /// 나무 타일 - 파괴 불가능한 장애물
        /// </summary>
        Wood = 5
    }
}