﻿
#define PROJECTION_CLEAR_VALUE 0xFFFFFFFF

// 四种坐标轴, 0 X↓Y→; 1 X↑Y←; 2 X→Y↑; 3 X←Y↓
// 保证Y轴上坐标值是最大的，且是正的
uint GetPackingBasisIndexTwoBits (int2 PixelOffset)
{
	if (abs(PixelOffset.x) >= abs(PixelOffset.y))
		return PixelOffset.x >= 0 ? 0 : 1;
	return PixelOffset.y >= 0 ? 2 : 3;
}
const static int4 BasisSnappedMatrices[4] = { int4(0, -1, 1, 0) , int4(0, 1, -1, 0), int4(1, 0, 0, 1), int4(-1, 0, 0, -1) };

// Encode offset for ' projection buffer ' storage
uint EncodeProjectionBufferValue(int2 PixelOffset)
{
	// build snapped basis
	uint PackingBasisIndex = GetPackingBasisIndexTwoBits(PixelOffset);
	
	// transform both parts to snapped basis
	int2 TransformedPixelOffset = int2(dot(BasisSnappedMatrices[PackingBasisIndex].xy, PixelOffset), dot(BasisSnappedMatrices[PackingBasisIndex].zw, PixelOffset));

	uint EncodeValue = 0;

	// pack whole part
	EncodeValue = ((TransformedPixelOffset.y << 12) | (abs(TransformedPixelOffset.x) << 1) | (TransformedPixelOffset.x >= 0 ? 1 : 0)) << 2;

	// pack basis part
	EncodeValue += PackingBasisIndex;

	return EncodeValue;
}

int2 DecodeProjectionBufferValue(uint EncodeValue)
{
	// unpack basis part
	uint PackingBasisIndex = EncodeValue & 3;
	EncodeValue = EncodeValue >> 2;

	int2 PixelOffset;
	// unpack whole part
	PixelOffset.x = ((EncodeValue & 1) == 1 ? 1 : -1) * ((EncodeValue >> 1) & 2047);
	EncodeValue = EncodeValue >> 12;
	PixelOffset.y = EncodeValue;
            
	PixelOffset = int2(dot(PixelOffset, BasisSnappedMatrices[PackingBasisIndex].xz), dot(PixelOffset, BasisSnappedMatrices[PackingBasisIndex].yw));
	return PixelOffset;
}