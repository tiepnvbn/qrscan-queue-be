# 🚀 Quick Deploy Checklist

## Backend (Render)
1. ✅ Tạo PostgreSQL database trên Render (Free plan)
2. ✅ Copy "Internal Database URL"
3. ✅ Tạo Web Service từ repo `qrscan-queue-be`
4. ✅ Set environment variables:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `QUEUEQR_DB_PROVIDER=Postgres`
   - `DATABASE_URL=<paste database URL>`
   - `CORS_ALLOWED_ORIGINS=https://your-app.netlify.app`
5. ✅ Deploy và copy Service URL

## Frontend (Netlify)
1. ✅ Deploy từ repo `qrscan-queue-fe`
2. ✅ Set environment variable:
   - `VITE_API_BASE_URL=<paste backend URL từ Render>`
3. ✅ Deploy và copy Site URL
4. ✅ Quay lại Render, update `CORS_ALLOWED_ORIGINS` với Site URL thực

## Test
- ✅ Mở frontend URL
- ✅ Lấy số (Take Ticket)
- ✅ Mở tab mới Staff Room → Kiểm tra realtime update

📖 Chi tiết xem file **DEPLOYMENT.md**
