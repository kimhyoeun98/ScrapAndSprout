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
-- Dumping data for table `player`
--

LOCK TABLES `player` WRITE;
/*!40000 ALTER TABLE `player` DISABLE KEYS */;
INSERT INTO `player` (`PLAYER_ID`, `PLAYER_PW`, `NICKNAME`, `player_pos_x`, `GOLD`, `BATTERY_GAUGE`, `player_pos_y`, `TOTAL_GOLD`, `MINIGAME_COUNT`, `CHARACTER_TYPE`, `EMAIL`) VALUES ('0','1234','seesese','0',10,100,'0',0,0,NULL,NULL),('alpha','1234','alpha','0',39,100,'0',0,0,NULL,NULL),('beta','1234','beta','0',0,100,'0',0,0,NULL,NULL),('delta','1234','delta','0',0,100,'0',0,0,NULL,NULL),('gamma','1234','gamma','0',20,100,'0',0,0,NULL,NULL),('test','1234','test','0',18,100,'0',0,0,NULL,NULL),('test2','1234','baby','0',353,100,'0',0,0,NULL,NULL),('test4','1234','test4','0',24,100,'0',0,0,NULL,'test4@naver.com'),('test5','1234','test5','0',0,100,'0',0,0,NULL,'alswp8264@naver.com'),('test6','1234','test6','0',0,100,'0',0,0,NULL,'alswp9856@nate.com');
/*!40000 ALTER TABLE `player` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Dumping data for table `room`
--

LOCK TABLES `room` WRITE;
/*!40000 ALTER TABLE `room` DISABLE KEYS */;
INSERT INTO `room` (`ROOM_ID`, `PLAYER_ID`, `IS_HOST`) VALUES ('room001','alpha',1),('room002','beta',0),('room003','gamma',0),('room004','delta',0);
/*!40000 ALTER TABLE `room` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Dumping data for table `map`
--

LOCK TABLES `map` WRITE;
/*!40000 ALTER TABLE `map` DISABLE KEYS */;
INSERT INTO `map` (`MAP_ID`, `ROOM_ID`, `MAP_TYPE`, `MAP_SEED`, `NPC`, `MAP_POS_X`, `WEATHER_CHANCE`, `TREE_COUNT`, `DECO_SCORE`) VALUES ('mapsafe001','room001','SAFE','SEED001','NPC_MOSS','500',70,3,210),('maptrash001','room001','TRASH','SEED001',NULL,'0',85,0,0);
/*!40000 ALTER TABLE `map` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Dumping data for table `inventory`
--

LOCK TABLES `inventory` WRITE;
/*!40000 ALTER TABLE `inventory` DISABLE KEYS */;
INSERT INTO `inventory` (`ITEM_ID`, `PLAYER_ID`, `MAP_ID`, `TRASH`, `SEED`, `BATTERY`, `DECO`) VALUES ('item001','alpha','maptrash001',5,2,1,0),('item002','beta','maptrash001',2,0,2,1),('item003','gamma','maptrash001',8,3,0,2),('item004','delta','maptrash001',0,1,3,0),('item005','alpha','mapsafe001',0,0,0,3),('item006','gamma','mapsafe001',0,0,0,2);
/*!40000 ALTER TABLE `inventory` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Dumping data for table `deco_detail`
--

LOCK TABLES `deco_detail` WRITE;
/*!40000 ALTER TABLE `deco_detail` DISABLE KEYS */;
INSERT INTO `deco_detail` (`DETAIL_ID`, `MAP_ID`, `PLAYER_ID`, `ITEM_ID`, `DECO_SCORE`, `DECO_POS_X`, `PLACED_AT`, `QUANTITY`) VALUES ('deco001','mapsafe001','alpha','item005',20,'120','2026-06-04 05:00:00',2),('deco002','mapsafe001','alpha','item005',50,'200','2026-06-04 05:05:00',1),('deco003','mapsafe001','gamma','item006',30,'350','2026-06-04 05:10:00',1),('deco004','mapsafe001','gamma','item006',20,'420','2026-06-04 05:15:00',1);
/*!40000 ALTER TABLE `deco_detail` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Dumping data for table `achievement`
--

LOCK TABLES `achievement` WRITE;
/*!40000 ALTER TABLE `achievement` DISABLE KEYS */;
INSERT INTO `achievement` (`ACHIEVEMENT_ID`, `PLAYER_ID`, `DESIGNATION`, `DETAIL`, `ACHIEVED_AT`) VALUES ('ach001','alpha','채굴 입문','미니게임 성공 10회','2026-06-04 04:00:00'),('ach002','alpha','골드 1K','누적 골드 1,000 달성','2026-06-04 04:30:00'),('ach003','gamma','채굴 고수','미니게임 성공 20회','2026-06-04 04:45:00'),('ach004','gamma','인테리어 입문','꾸미기 점수 100 달성','2026-06-04 05:20:00'),('ach005','beta','골드 1K','누적 골드 1,000 달성','2026-06-04 03:50:00');
/*!40000 ALTER TABLE `achievement` ENABLE KEYS */;
UNLOCK TABLES;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2026-07-01 22:10:50
