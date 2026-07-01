-- MySQL dump 10.13  Distrib 8.0.45, for Win64 (x86_64)
--
-- Host: 127.0.0.1    Database: scrap_sprout
-- ------------------------------------------------------
-- Server version	8.0.45

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `player`
--

DROP TABLE IF EXISTS `player`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `player` (
  `PLAYER_ID` varchar(250) NOT NULL COMMENT '플레이어 고유 ID (UUID)',
  `PLAYER_PW` varchar(255) NOT NULL COMMENT '암호화된 비밀번호 (BCrypt)',
  `NICKNAME` varchar(50) NOT NULL COMMENT '플레이어 닉네임',
  `player_pos_x` varchar(100) NOT NULL DEFAULT '0',
  `GOLD` int NOT NULL DEFAULT '0' COMMENT '보유 골드',
  `BATTERY_GAUGE` float NOT NULL DEFAULT '100' COMMENT '현재 배터리 잔량 (0~100)',
  `player_pos_y` varchar(100) NOT NULL DEFAULT '0',
  `TOTAL_GOLD` int NOT NULL DEFAULT '0' COMMENT '게임 전체 누적 획득 골드',
  `MINIGAME_COUNT` int NOT NULL DEFAULT '0' COMMENT '채굴 미니게임 성공 횟수',
  `CHARACTER_TYPE` varchar(20) DEFAULT NULL COMMENT '선택한 캐릭터 타입',
  `EMAIL` varchar(100) DEFAULT NULL COMMENT '이메일',
  PRIMARY KEY (`PLAYER_ID`),
  UNIQUE KEY `uq_nickname` (`NICKNAME`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='플레이어 테이블';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `room`
--

DROP TABLE IF EXISTS `room`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `room` (
  `ROOM_ID` varchar(250) NOT NULL COMMENT '방 식별자',
  `PLAYER_ID` varchar(250) NOT NULL COMMENT '참여한 플레이어 FK',
  `IS_HOST` tinyint NOT NULL DEFAULT '0' COMMENT '방장 여부 1=방장',
  PRIMARY KEY (`ROOM_ID`),
  KEY `fk_room_player` (`PLAYER_ID`),
  CONSTRAINT `fk_room_player` FOREIGN KEY (`PLAYER_ID`) REFERENCES `player` (`PLAYER_ID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `map`
--

DROP TABLE IF EXISTS `map`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `map` (
  `MAP_ID` varchar(250) NOT NULL COMMENT '맵 식별자',
  `ROOM_ID` varchar(250) NOT NULL COMMENT '방 FK',
  `MAP_TYPE` varchar(20) NOT NULL COMMENT '맵 종류 TRASH/SAFE',
  `MAP_SEED` varchar(100) NOT NULL COMMENT '맵 생성 시드값',
  `NPC` varchar(100) DEFAULT NULL COMMENT '배치된 NPC',
  `MAP_POS_X` varchar(100) NOT NULL DEFAULT '0' COMMENT '맵 x좌표',
  `WEATHER_CHANCE` float NOT NULL DEFAULT '100' COMMENT '기상 이벤트 발생 확률(%)',
  `TREE_COUNT` int NOT NULL DEFAULT '0' COMMENT '세이프 맵에 심은 나무 수',
  `DECO_SCORE` int NOT NULL DEFAULT '0' COMMENT '세이프 맵 꾸미기 점수 합산',
  PRIMARY KEY (`MAP_ID`),
  KEY `fk_map_room` (`ROOM_ID`),
  CONSTRAINT `fk_map_room` FOREIGN KEY (`ROOM_ID`) REFERENCES `room` (`ROOM_ID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `inventory`
--

DROP TABLE IF EXISTS `inventory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `inventory` (
  `ITEM_ID` varchar(250) NOT NULL COMMENT '아이템 고유 ID (UUID)',
  `PLAYER_ID` varchar(250) NOT NULL COMMENT '보유 플레이어 FK',
  `MAP_ID` varchar(250) NOT NULL COMMENT '획득한 맵 FK',
  `TRASH` int NOT NULL DEFAULT '0' COMMENT '보유 쓰레기 총 수량',
  `SEED` int NOT NULL DEFAULT '0' COMMENT '보유 씨앗 수량',
  `BATTERY` int NOT NULL DEFAULT '0' COMMENT '보유 배터리 수량',
  `DECO` int NOT NULL DEFAULT '0' COMMENT '보유 꾸미기 아이템 총 수량',
  PRIMARY KEY (`ITEM_ID`),
  UNIQUE KEY `uq_player_map` (`PLAYER_ID`,`MAP_ID`),
  KEY `fk_inv_map` (`MAP_ID`),
  CONSTRAINT `fk_inv_map` FOREIGN KEY (`MAP_ID`) REFERENCES `map` (`MAP_ID`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_inv_player` FOREIGN KEY (`PLAYER_ID`) REFERENCES `player` (`PLAYER_ID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `deco_detail`
--

DROP TABLE IF EXISTS `deco_detail`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `deco_detail` (
  `DETAIL_ID` varchar(250) NOT NULL COMMENT '배치 행 식별자',
  `MAP_ID` varchar(250) NOT NULL COMMENT '배치한 맵 FK',
  `PLAYER_ID` varchar(250) NOT NULL COMMENT '배치한 플레이어 FK',
  `ITEM_ID` varchar(250) NOT NULL COMMENT '인벤토리 FK',
  `DECO_SCORE` int NOT NULL DEFAULT '0' COMMENT '보정 적용된 실제 꾸미기 점수',
  `DECO_POS_X` varchar(100) NOT NULL DEFAULT '0' COMMENT '세이프 맵 배치 x좌표',
  `PLACED_AT` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '배치 시각',
  `QUANTITY` int NOT NULL DEFAULT '1' COMMENT '배치 수량',
  PRIMARY KEY (`DETAIL_ID`),
  KEY `fk_deco_map` (`MAP_ID`),
  KEY `fk_deco_player` (`PLAYER_ID`),
  KEY `fk_deco_item` (`ITEM_ID`),
  CONSTRAINT `fk_deco_item` FOREIGN KEY (`ITEM_ID`) REFERENCES `inventory` (`ITEM_ID`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_deco_map` FOREIGN KEY (`MAP_ID`) REFERENCES `map` (`MAP_ID`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_deco_player` FOREIGN KEY (`PLAYER_ID`) REFERENCES `player` (`PLAYER_ID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `achievement`
--

DROP TABLE IF EXISTS `achievement`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `achievement` (
  `ACHIEVEMENT_ID` varchar(250) NOT NULL COMMENT '업적 행 식별자',
  `PLAYER_ID` varchar(250) NOT NULL COMMENT '달성한 플레이어 FK',
  `DESIGNATION` varchar(100) DEFAULT NULL COMMENT '부여된 칭호',
  `DETAIL` varchar(255) DEFAULT NULL COMMENT '업적 달성 조건 설명',
  `ACHIEVED_AT` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '달성 시각',
  PRIMARY KEY (`ACHIEVEMENT_ID`),
  KEY `fk_ach_player` (`PLAYER_ID`),
  CONSTRAINT `fk_ach_player` FOREIGN KEY (`PLAYER_ID`) REFERENCES `player` (`PLAYER_ID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2026-07-01 22:10:37
