package com.example.demo.util;

import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.SignatureAlgorithm;
import io.jsonwebtoken.security.Keys;
import org.springframework.stereotype.Component;
import java.security.Key;
import java.util.Date;

@Component
public class JwtUtil {
    // 256비트 이상의 비밀키 (보안상 아주 중요합니다!)
    private final Key key = Keys.secretKeyFor(SignatureAlgorithm.HS256);
    private final long expirationTime = 1000 * 60 * 60 * 10; // 10시간 유효

    public String generateToken(int userId, String userName) {
        return Jwts.builder()
                .setSubject(userName)
                .claim("userId", userId) // 토큰 안에 유저 ID를 숨겨둡니다.
                .setIssuedAt(new Date())
                .setExpiration(new Date(System.currentTimeMillis() + expirationTime))
                .signWith(key)
                .compact();
    }
}