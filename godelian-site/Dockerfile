
# Use Ubuntu as the base image
FROM ubuntu:22.04 AS build

# Install Node.js, npm, and other dependencies
RUN apt-get update \
    && apt-get install -y curl gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_18.x | bash - \
    && apt-get install -y nodejs \
    && npm install -g @angular/cli \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY . .

# Build the Angular application for production
RUN npm run build

# Use Ubuntu as the base image for the final image
FROM ubuntu:22.04 AS final

# Install nginx
RUN apt-get update && apt-get install -y nginx && apt-get clean && rm -rf /var/lib/apt/lists/*

# Copy built application from build stage
COPY --from=build /app/dist/godelian-site/browser /var/www/html

# Copy custom nginx configuration
COPY ./ssl/default /etc/nginx/sites-available/default

# Expose HTTP and HTTPS ports
EXPOSE 80 443

# Start nginx
CMD ["nginx", "-g", "daemon off;"]
