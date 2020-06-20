docker build -t img .
docker run -d --name imageserver -p 5000:80 -v /www/wwwroot/images.tc.clofresh.cn/images:/app/wwwroot/upload img:latest