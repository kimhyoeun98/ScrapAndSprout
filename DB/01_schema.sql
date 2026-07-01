-- =====================================================================
--  01_schema.sql : 데이터베이스(스키마) 생성
--  ScrapAndSprout DB  |  MySQL 8.0+
--
--  DB 이름을 바꾸려면 아래 scrap_sprout 두 곳을 원하는 이름으로 수정하세요.
-- =====================================================================

CREATE DATABASE IF NOT EXISTS scrap_sprout
  DEFAULT CHARACTER SET utf8mb4
  DEFAULT COLLATE utf8mb4_0900_ai_ci;

USE scrap_sprout;
