rm -rf SkeleArtifact/bin
rm -rf SkeleArtifact/obj
dotnet restore
dotnet build
mkdir SkeleBuild
cp SkeleArtifact/bin/Debug/netstandard2.0/* SkeleBuild/
cp bundle/skelebundle SkeleBuild/
cp icon.png SkeleBuild/
cp manifest.json SkeleBuild/
cp README.md SkeleBuild/
cd SkeleBuild/
zip skelefact.zip *
cd ..
cp -r SkeleBuild ~/.config/r2modmanPlus-local/RiskOfRain2/profiles/testing/BepInEx/plugins/