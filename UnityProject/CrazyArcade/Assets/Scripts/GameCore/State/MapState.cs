using System.Collections.Generic;

namespace CrazyArcade.GameCore
{

    /// <summary>
    /// 맵의 타일 정보를 저장하는 클래스
    /// - 2D 그리드 구조
    /// - 각 타일의 타입 저장
    /// - 서버/클라이언트 공통 사용
    /// </summary>
    public class MapState
    {
        // 맵 크기
        public int Width { get; private set; }
        public int Height { get; private set; }

        // 타일 데이터 (2D 배열)
        private TileType[,] tiles;  // ← TileType을 바로 사용!

        /// <summary>
        /// 맵 생성자
        /// </summary>
        /// <param name="width">맵 가로 크기</param>
        /// <param name="height">맵 세로 크기</param>
        public MapState(int width, int height)
        {
            Width = width;
            Height = height;
            tiles = new TileType[width, height];

            // 기본값: 모든 타일을 Ground로 초기화
            InitializeGround();
        }

        /// <summary>
        /// 모든 타일을 Ground로 초기화
        /// </summary>
        private void InitializeGround()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    tiles[x, y] = TileType.Ground;
                }
            }
        }

        /// <summary>
        /// 특정 위치의 타일 타입 가져오기
        /// </summary>
        /// <param name="pos">타일 위치 (Int2)</param>
        /// <returns>해당 위치의 TileType</returns>
        public TileType GetTile(Int2 pos)  // ← Int2를 바로 사용!
        {
            // 맵 범위 체크
            if (!IsInBounds(pos))
            {
                return TileType.Wall; // 맵 밖은 벽으로 처리
            }

            return tiles[pos.X, pos.Y];
        }

        /// <summary>
        /// 특정 위치에 타일 설정
        /// </summary>
        /// <param name="pos">타일 위치 (Int2)</param>
        /// <param name="tileType">설정할 타일 타입</param>
        public void SetTile(Int2 pos, TileType tileType)
        {
            // 맵 범위 체크
            if (!IsInBounds(pos))
            {
                return;
            }

            tiles[pos.X, pos.Y] = tileType;
        }

        /// <summary>
        /// 좌표가 맵 범위 내에 있는지 체크
        /// </summary>
        /// <param name="pos">체크할 위치</param>
        /// <returns>범위 내면 true, 아니면 false</returns>
        public bool IsInBounds(Int2 pos)
        {
            return pos.X >= 0 && pos.X < Width &&
                   pos.Y >= 0 && pos.Y < Height;
        }

        /// <summary>
        /// 해당 위치로 이동 가능한지 체크
        /// </summary>
        /// <param name="pos">체크할 위치</param>
        /// <returns>이동 가능하면 true</returns>
        public bool IsWalkable(Int2 pos)
        {
            if (!IsInBounds(pos))
            {
                return false;
            }

            TileType tile = GetTile(pos);

            // Ground만 이동 가능
            return tile == TileType.Ground;
        }

        /// <summary>
        /// 해당 타일이 파괴 가능한지 체크
        /// </summary>
        /// <param name="pos">체크할 위치</param>
        /// <returns>파괴 가능하면 true</returns>
        public bool IsDestructible(Int2 pos)
        {
            if (!IsInBounds(pos))
            {
                return false;
            }

            TileType tile = GetTile(pos);

            // 산호초,물 타일들만 파괴 가능
            return tile == TileType.Coral1 || 
                  tile == TileType.Coral2 ||
                  tile == TileType.Water;
        }

        /// <summary>
        /// 물 타일인지 체크 (연쇄 폭발용)
        /// </summary>
        /// <param name="pos">체크할 위치</param>
        /// <returns>물 타일이면 true</returns>
        public bool IsWaterTile(Int2 pos)
        {
            if (!IsInBounds(pos))
            {
                return false;
            }

            return GetTile(pos) == TileType.Water;
        }

        /// <summary>
        /// 모든 물 타일 위치 가져오기 (상어 스폰용)
        /// </summary>
        /// <returns>물 타일 위치 리스트</returns>
        public List<Int2> GetAllWaterTiles()
        {
            List<Int2> waterTiles = new List<Int2>();

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (tiles[x, y] == TileType.Water)
                    {
                        waterTiles.Add(new Int2(x, y));
                    }
                }
            }

            return waterTiles;
        }

        /// <summary>
        /// 디버그용 맵 출력
        /// </summary>
        /// <returns>맵 문자열</returns>
        public string ToDebugString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            for (int y = Height - 1; y >= 0; y--) // 위에서 아래로
            {
                for (int x = 0; x < Width; x++)
                {
                    TileType tile = tiles[x, y];

                    // 타일 종류별 문자 표시
                    switch (tile)
                    {
                        case TileType.Ground:
                            sb.Append("□ ");
                            break;
                        case TileType.Water:
                            sb.Append("🌊");
                            break;
                        case TileType.Wall:
                            sb.Append("■ ");
                            break;
                        case TileType.Coral1:
                            sb.Append("🪸");  // 산호초 1
                            break;
                        case TileType.Coral2:
                            sb.Append("🐚");  // 산호초 2 (조개)
                            break;
                        case TileType.Wood:
                            sb.Append("🌲");
                            break;
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}